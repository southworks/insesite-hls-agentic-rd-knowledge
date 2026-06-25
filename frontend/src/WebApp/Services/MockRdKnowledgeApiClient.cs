using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;
using Cohere.AgenticRDKnowledge.Shared.Contracts.VectorDb;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public sealed class MockRdKnowledgeApiClient(
    DatasetSeedCatalogService catalog,
    MockWorkflowSimulator simulator) : IRdKnowledgeApiClient
{
    public Task<VectorDbStoreSummary> GetVectorDbStoreSummaryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(simulator.GetVectorDbSummary());

    public Task<StudyDocumentsResponse> GetStudyDocumentsAsync(string studyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(catalog.GetStudyDocuments(studyId));

    public Task<StartIngestionWorkflowResponse> StartIngestionWorkflowAsync(string studyId, CancellationToken cancellationToken = default)
    {
        var executionId = $"ing-{Guid.NewGuid():N}"[..12];
        simulator.StartIngestion(executionId, studyId);
        return Task.FromResult(new StartIngestionWorkflowResponse(executionId, studyId, WorkflowStatus.Running));
    }

    public Task<IngestionWorkflowProgress> GetIngestionStatusAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var state = simulator.GetExecution(executionId)
            ?? throw new KeyNotFoundException($"Ingestion execution {executionId} not found.");

        if (state.Status is WorkflowStatus.Running or WorkflowStatus.Pending)
        {
            state = simulator.AdvanceOnPoll(executionId);
        }

        var stage = simulator.GetIngestionStage(state);
        var scenario = catalog.GetScenarioByStudyId(state.ResourceId, WorkflowBlock.Ingestion);
        var study = scenario?.Study;

        IngestionTranslationResult? translation = null;
        MetadataLinkingResult? linking = null;

        if (stage >= IngestionStage.IngestionTranslation)
        {
            var json = catalog.ReadAgentOutputJson(state.ResourceId, "ingestion-translation.json");
            translation = AgentOutputParser.ParseIngestionTranslation(json);
        }

        if (stage >= IngestionStage.MetadataLinking)
        {
            var json = catalog.ReadAgentOutputJson(state.ResourceId, "metadata-linking.json");
            linking = AgentOutputParser.ParseMetadataLinking(json);
        }

        var allowedActions = new List<string>();
        if (state.Status == WorkflowStatus.AwaitingHumanApproval)
        {
            allowedActions.Add("SubmitDecision");
        }

        var message = stage switch
        {
            IngestionStage.IngestionTranslation => "Reading raw R&D knowledge from Microsoft Fabric and normalizing formats…",
            IngestionStage.MetadataLinking => "Extracting entities and linking documents to datasets and studies…",
            IngestionStage.HumanApproval => "Knowledge Curator: review ingested content before writing to Vector DB.",
            IngestionStage.Completed => "Ingestion approved. Knowledge embedded and saved to Vector DB.",
            IngestionStage.Failed => "Ingestion denied or failed.",
            _ => "Preparing ingestion workflow…"
        };

        return Task.FromResult(new IngestionWorkflowProgress(
            executionId,
            state.ResourceId,
            state.Status,
            stage,
            message,
            study,
            translation,
            linking,
            simulator.BuildRetrievalTrace(state.PollCount, "Ingestion linking"),
            state.HumanDecision,
            allowedActions));
    }

    public Task<SubmitIngestionDecisionResponse> SubmitIngestionDecisionAsync(
        string executionId,
        SubmitIngestionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        simulator.SubmitDecision(executionId, request.Approved, request.Notes);
        var state = simulator.GetExecution(executionId)!;
        return Task.FromResult(new SubmitIngestionDecisionResponse(executionId, state.Status));
    }

    public Task<QuerySessionState> GetQuerySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = simulator.GetQuerySession(sessionId);
        if (session is null)
        {
            return Task.FromResult(BuildEmptyQuerySession(sessionId));
        }

        if (session.IsChatRunning)
        {
            simulator.AdvanceChatOnPoll(sessionId);
            if (!session.IsChatRunning)
            {
                AppendAssistantMessage(session);
            }
        }

        return Task.FromResult(BuildQuerySessionState(session));
    }

    public Task<QuerySessionState> SendChatMessageAsync(
        string sessionId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        simulator.BeginChatTurn(sessionId, request.Question, request.StudyScope);
        var session = simulator.GetQuerySession(sessionId)!;
        return Task.FromResult(BuildQuerySessionState(session));
    }

    public Task<StartCurationResponse> StartCurationAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = simulator.GetQuerySession(sessionId)
            ?? throw new InvalidOperationException($"Query session {sessionId} has no chat messages.");

        if (session.Messages.Count == 0)
        {
            throw new InvalidOperationException("Cannot curate without accumulated chat responses.");
        }

        if (session.CurationExecutionId is not null)
        {
            var existing = simulator.GetExecution(session.CurationExecutionId);
            if (existing?.Status is WorkflowStatus.Running or WorkflowStatus.AwaitingHumanApproval)
            {
                throw new InvalidOperationException("Curation is already in progress for this session.");
            }
        }

        var state = simulator.StartCuration(sessionId);
        return Task.FromResult(new StartCurationResponse(state.ExecutionId, sessionId, WorkflowStatus.Running));
    }

    public Task<CurationWorkflowProgress> GetCurationStatusAsync(string curationExecutionId, CancellationToken cancellationToken = default)
    {
        var state = simulator.GetExecution(curationExecutionId)
            ?? throw new KeyNotFoundException($"Curation execution {curationExecutionId} not found.");

        if (state.Status is WorkflowStatus.Running or WorkflowStatus.Pending)
        {
            state = simulator.AdvanceOnPoll(curationExecutionId);
        }

        var stage = simulator.GetCurationStage(state);
        var session = simulator.GetQuerySession(state.ResourceId);
        var studyId = session?.StudyScope ?? "abc-2024";

        CurationComplianceResult? curation = null;
        if (stage >= QueryStage.CurationRunning && stage != QueryStage.Pending)
        {
            var json = catalog.ReadAgentOutputJson(studyId, "curation-compliance.json");
            curation = AgentOutputParser.ParseCurationCompliance(json);
        }

        var allowedActions = new List<string>();
        if (state.Status == WorkflowStatus.AwaitingHumanApproval)
        {
            allowedActions.Add("SubmitDecision");
        }

        var message = stage switch
        {
            QueryStage.CurationRunning => "Curation & Compliance reviewing accumulated chat responses…",
            QueryStage.AwaitingComplianceReview => "Compliance Reviewer: review curation flags and captured decisions.",
            QueryStage.Completed => "Curation cycle approved and audited.",
            QueryStage.Failed => "Curation cycle denied or failed.",
            _ => "Preparing curation…"
        };

        return Task.FromResult(new CurationWorkflowProgress(
            curationExecutionId,
            state.ResourceId,
            state.Status,
            stage,
            message,
            curation,
            state.HumanDecision,
            allowedActions));
    }

    public Task<SubmitQueryDecisionResponse> SubmitCurationDecisionAsync(
        string curationExecutionId,
        SubmitQueryDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        simulator.SubmitDecision(curationExecutionId, request.Approved, request.Notes);
        var state = simulator.GetExecution(curationExecutionId)!;
        return Task.FromResult(new SubmitQueryDecisionResponse(curationExecutionId, state.Status));
    }

    public Task<bool> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    private void AppendAssistantMessage(MockQuerySessionState session)
    {
        var studyId = session.StudyScope ?? "abc-2024";
        var json = catalog.ReadAgentOutputJson(studyId, "search-chat.json");
        var result = AgentOutputParser.ParseSearchChat(json);
        if (result is null)
        {
            return;
        }

        session.Messages.Add(new ChatMessage(
            "assistant",
            result.Answer,
            result.Citations,
            result.LineageSummary,
            simulator.BuildRetrievalTrace(session.ChatPollCount, "Query retrieval"),
            DateTimeOffset.UtcNow));
    }

    private static QuerySessionState BuildEmptyQuerySession(string sessionId) =>
        new(
            sessionId,
            null,
            [],
            false,
            null,
            WorkflowStatus.Pending,
            QueryStage.Pending,
            "Ask a research question to begin Search & Chat.",
            null,
            null,
            []);

    private QuerySessionState BuildQuerySessionState(MockQuerySessionState session)
    {
        var stage = simulator.GetQuerySessionStage(session);
        WorkflowStatus curationStatus = WorkflowStatus.Pending;
        CurationComplianceResult? curation = null;
        HumanDecisionRecord? humanDecision = null;
        var allowedActions = new List<string>();

        if (session.CurationExecutionId is not null)
        {
            var curationState = simulator.GetExecution(session.CurationExecutionId);
            if (curationState is not null)
            {
                curationStatus = curationState.Status;
                humanDecision = curationState.HumanDecision;
                stage = simulator.GetCurationStage(curationState);

                if (curationState.Status is WorkflowStatus.Running or WorkflowStatus.AwaitingHumanApproval or WorkflowStatus.Completed)
                {
                    var studyId = session.StudyScope ?? "abc-2024";
                    var json = catalog.ReadAgentOutputJson(studyId, "curation-compliance.json");
                    curation = AgentOutputParser.ParseCurationCompliance(json);
                }

                if (curationState.Status == WorkflowStatus.AwaitingHumanApproval)
                {
                    allowedActions.Add("SubmitDecision");
                }
            }
        }

        var message = session.IsChatRunning
            ? "Search & Chat retrieving grounded evidence from Vector DB…"
            : stage switch
            {
                QueryStage.ChatActive => "Search & Chat active — ask follow-up questions or click Curate when ready.",
                QueryStage.CurationRunning => "Curation & Compliance reviewing accumulated chat responses…",
                QueryStage.AwaitingComplianceReview => "Compliance Reviewer: review curation flags and captured decisions.",
                QueryStage.Completed => "Curation cycle approved and audited.",
                QueryStage.Failed => "Curation cycle denied or failed.",
                _ => session.Messages.Count > 0
                    ? "Search & Chat active — ask follow-up questions or click Curate when ready."
                    : "Ask a research question to begin Search & Chat."
            };

        return new QuerySessionState(
            session.SessionId,
            session.StudyScope,
            session.Messages.ToList(),
            session.IsChatRunning,
            session.CurationExecutionId,
            curationStatus,
            stage,
            message,
            curation,
            humanDecision,
            allowedActions);
    }
}
