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

    public Task<StartQueryWorkflowResponse> StartQueryWorkflowAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var executionId = $"qry-{Guid.NewGuid():N}"[..12];
        simulator.StartQueryWorkflow(executionId, sessionId);
        return Task.FromResult(new StartQueryWorkflowResponse(executionId, sessionId, WorkflowStatus.Pending));
    }

    public Task<QuerySessionState> GetQuerySessionAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var session = simulator.GetQuerySession(executionId);
        if (session is null)
        {
            throw new KeyNotFoundException($"Query execution {executionId} not found.");
        }

        if (session.IsChatRunning)
        {
            simulator.AdvanceChatOnPoll(executionId);
            if (!session.IsChatRunning)
            {
                AppendAssistantMessage(session);
            }
        }

        return Task.FromResult(BuildQuerySessionState(session));
    }

    public Task<QuerySessionState> SendChatMessageAsync(
        string executionId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        simulator.BeginChatTurn(executionId, request.Question, request.StudyScope);
        var session = simulator.GetQuerySession(executionId)!;
        return Task.FromResult(BuildQuerySessionState(session));
    }

    public Task<StartCurationResponse> StartCurationAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var session = simulator.GetQuerySession(executionId)
            ?? throw new InvalidOperationException($"Query execution {executionId} not found.");

        if (session.Messages.Count == 0)
        {
            throw new InvalidOperationException("Cannot curate without accumulated chat responses.");
        }

        if (session.CurationStarted)
        {
            var existing = simulator.GetExecution(executionId);
            if (existing?.Status is WorkflowStatus.Running or WorkflowStatus.AwaitingHumanApproval)
            {
                throw new InvalidOperationException("Curation is already in progress for this execution.");
            }
        }

        var state = simulator.StartCuration(executionId);
        return Task.FromResult(new StartCurationResponse(state.ExecutionId, session.SessionId, WorkflowStatus.Running));
    }

    public Task<CurationWorkflowProgress> GetCurationStatusAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var session = simulator.GetQuerySession(executionId)
            ?? throw new KeyNotFoundException($"Query execution {executionId} not found.");

        if (!session.CurationStarted)
        {
            throw new InvalidOperationException($"Curation has not started for execution {executionId}.");
        }

        var state = simulator.GetExecution(executionId)
            ?? throw new KeyNotFoundException($"Query execution {executionId} not found.");

        if (state.Status is WorkflowStatus.Running or WorkflowStatus.Pending)
        {
            state = simulator.AdvanceOnPoll(executionId);
        }

        var stage = simulator.GetCurationStage(state);
        var studyId = session.StudyScope ?? "abc-2024";

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
            executionId,
            session.SessionId,
            state.Status,
            stage,
            message,
            curation,
            state.HumanDecision,
            allowedActions));
    }

    public Task<SubmitQueryDecisionResponse> SubmitCurationDecisionAsync(
        string executionId,
        SubmitQueryDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        simulator.SubmitDecision(executionId, request.Approved, request.Notes);
        var state = simulator.GetExecution(executionId)!;
        return Task.FromResult(new SubmitQueryDecisionResponse(executionId, state.Status));
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

    private QuerySessionState BuildQuerySessionState(MockQuerySessionState session)
    {
        var execution = simulator.GetExecution(session.ExecutionId);
        var stage = simulator.GetQuerySessionStage(session, execution);
        WorkflowStatus curationStatus = WorkflowStatus.Pending;
        CurationComplianceResult? curation = null;
        HumanDecisionRecord? humanDecision = null;
        var allowedActions = new List<string>();

        if (session.CurationStarted && execution is not null)
        {
            curationStatus = execution.Status;
            humanDecision = execution.HumanDecision;
            stage = simulator.GetCurationStage(execution);

            if (execution.Status is WorkflowStatus.Running or WorkflowStatus.AwaitingHumanApproval or WorkflowStatus.Completed)
            {
                var studyId = session.StudyScope ?? "abc-2024";
                var json = catalog.ReadAgentOutputJson(studyId, "curation-compliance.json");
                curation = AgentOutputParser.ParseCurationCompliance(json);
            }

            if (execution.Status == WorkflowStatus.AwaitingHumanApproval)
            {
                allowedActions.Add("SubmitDecision");
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
            session.CurationStarted ? session.ExecutionId : null,
            curationStatus,
            stage,
            message,
            curation,
            humanDecision,
            allowedActions);
    }
}
