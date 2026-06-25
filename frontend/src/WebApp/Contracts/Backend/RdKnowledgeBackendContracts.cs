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
        "/api/rd-knowledge/query/workflow/start";

    public const string GetQueryStatus =
        "/api/rd-knowledge/executions/{executionId}/query/status";

    public const string SubmitQueryDecision =
        "/api/rd-knowledge/executions/{executionId}/query/resume";

    public const string GetStudyDocuments =
        "/api/rd-knowledge/studies/{studyId}/documents";

    public const string GetFabricStoreSummary =
        "/api/rd-knowledge/fabric/summary";
}
