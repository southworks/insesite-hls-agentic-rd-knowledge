using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Fabric;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public sealed class MockRdKnowledgeApiClient(
    DatasetSeedCatalogService catalog,
    MockWorkflowSimulator simulator) : IRdKnowledgeApiClient
{
    public Task<FabricStoreSummary> GetFabricStoreSummaryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(simulator.GetFabricSummary());

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
            IngestionStage.IngestionTranslation => "Connecting to study portals and normalizing source formats…",
            IngestionStage.MetadataLinking => "Extracting entities and linking documents to datasets and studies…",
            IngestionStage.HumanApproval => "Review ingested content before persistence to Microsoft Fabric.",
            IngestionStage.Completed => "Ingestion approved. Knowledge persisted to Microsoft Fabric.",
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
            simulator.BuildRetrievalTrace(state, "Ingestion linking"),
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

    public Task<StartQueryWorkflowResponse> StartQueryWorkflowAsync(StartQueryWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var executionId = $"qry-{Guid.NewGuid():N}"[..12];
        simulator.StartQuery(executionId, request.SessionId, request.Question, request.StudyScope);
        return Task.FromResult(new StartQueryWorkflowResponse(executionId, request.SessionId, WorkflowStatus.Running));
    }

    public Task<QueryWorkflowProgress> GetQueryStatusAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var state = simulator.GetExecution(executionId)
            ?? throw new KeyNotFoundException($"Query execution {executionId} not found.");

        if (state.Status is WorkflowStatus.Running or WorkflowStatus.Pending)
        {
            state = simulator.AdvanceOnPoll(executionId);
        }

        var stage = simulator.GetQueryStage(state);
        var studyId = state.StudyScope ?? "abc-2024";

        SearchChatResult? searchChat = null;
        CurationComplianceResult? curation = null;

        if (stage >= QueryStage.SearchChat)
        {
            var json = catalog.ReadAgentOutputJson(studyId, "search-chat.json");
            searchChat = AgentOutputParser.ParseSearchChat(json);
        }

        if (stage >= QueryStage.CurationCompliance)
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
            QueryStage.SearchChat => "Retrieving grounded evidence from Microsoft Fabric…",
            QueryStage.CurationCompliance => "Running curation and compliance checks on query results…",
            QueryStage.HumanApproval => "Review answers and compliance findings before finalization.",
            QueryStage.Completed => "Query cycle approved and audited.",
            QueryStage.Failed => "Query cycle denied or failed.",
            _ => "Preparing query workflow…"
        };

        return Task.FromResult(new QueryWorkflowProgress(
            executionId,
            state.ResourceId,
            state.Question ?? string.Empty,
            state.StudyScope,
            state.Status,
            stage,
            message,
            searchChat,
            curation,
            simulator.BuildRetrievalTrace(state, "Query retrieval"),
            state.HumanDecision,
            allowedActions));
    }

    public Task<SubmitQueryDecisionResponse> SubmitQueryDecisionAsync(
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
}
