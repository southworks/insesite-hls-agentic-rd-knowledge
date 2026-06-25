namespace Cohere.AgenticRDKnowledge.Shared.Contracts.Backend;

/// <summary>
/// Documented REST paths for future backend integration.
/// Implement in RdKnowledgeApiClient when UseMockBackend is false.
/// </summary>
public static class RdKnowledgeBackendRoutes
{
    public const string Health = "/health";

    public const string StartIngestionWorkflow =
        "/api/rd-knowledge/studies/{studyId}/ingestion/workflow/start";

    public const string GetIngestionStatus =
        "/api/rd-knowledge/executions/{executionId}/ingestion/status";

    public const string SubmitIngestionDecision =
        "/api/rd-knowledge/executions/{executionId}/ingestion/resume";

    public const string StartQueryWorkflow =
        "/api/rd-knowledge/query/sessions/{sessionId}/workflow/start";

    public const string GetQuerySession =
        "/api/rd-knowledge/executions/{executionId}/query/session";

    public const string SendChatMessage =
        "/api/rd-knowledge/executions/{executionId}/query/chat";

    public const string StartCuration =
        "/api/rd-knowledge/executions/{executionId}/query/curate";

    public const string GetCurationStatus =
        "/api/rd-knowledge/executions/{executionId}/query/status";

    public const string SubmitCurationDecision =
        "/api/rd-knowledge/executions/{executionId}/query/resume";

    public const string GetStudyDocuments =
        "/api/rd-knowledge/studies/{studyId}/documents";

    public const string GetVectorDbStoreSummary =
        "/api/rd-knowledge/vector-db/summary";
}
