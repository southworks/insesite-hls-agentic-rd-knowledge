namespace Cohere.AgenticRDKnowledge.Shared.Contracts.Backend;

/// <summary>
/// REST paths for Api.Host integration.
/// </summary>
public static class RdKnowledgeBackendRoutes
{
    public const string Health = "/health";

    public const string GetVectorDbSummary = "/api/rd-knowledge/vector-db/summary";

    public const string StartIngestion = "/api/rd-knowledge/ingestion/start";

    public const string GetIngestionStatus =
        "/api/rd-knowledge/ingestion/executions/{executionId}/status";

    public const string SubmitIngestionDecision =
        "/api/rd-knowledge/ingestion/sources/{sourceId}/executions/{executionId}/resume";

    public const string Ask = "/api/rd-knowledge/query/ask";

    public const string StartCurate = "/api/rd-knowledge/query/curate/start";

    public const string GetCurateStatus =
        "/api/rd-knowledge/query/curate/executions/{executionId}/status";

    public const string SubmitCurateDecision =
        "/api/rd-knowledge/query/curate/sessions/{sessionId}/executions/{executionId}/resume";
}
