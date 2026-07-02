using System.Text.Json.Serialization;

namespace CohereRndKnowledgeMining.Api.Host.Contracts;

// ---- Block 1: Ingestion ----

public sealed class StartIngestionRequest
{
    /// <summary>Identifier of the raw R&D knowledge batch to ingest.</summary>
    public required string SourceId { get; init; }

    /// <summary>Optional caller-provided execution id; generated when omitted.</summary>
    public string? ExecutionId { get; init; }
}

public sealed class IngestionProgressResponse
{
    [JsonPropertyName("executionId")]
    public required string ExecutionId { get; init; }

    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("status")]
    public required WorkflowStatus Status { get; init; }

    [JsonPropertyName("currentStage")]
    public IngestionStage CurrentStage { get; init; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }

    [JsonPropertyName("study")]
    public StudySummaryResponse? Study { get; init; }

    [JsonPropertyName("ingestionTranslation")]
    public IngestionTranslationResultResponse? IngestionTranslation { get; init; }

    [JsonPropertyName("metadataLinking")]
    public MetadataLinkingResultResponse? MetadataLinking { get; init; }

    [JsonPropertyName("retrievalTrace")]
    public IReadOnlyList<RetrievalTraceEventResponse>? RetrievalTrace { get; init; }

    [JsonPropertyName("humanDecision")]
    public HumanDecisionRecordResponse? HumanDecision { get; init; }

    [JsonPropertyName("allowedActions")]
    public IReadOnlyList<string> AllowedActions { get; init; } = [];

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; init; }
}

public sealed class StartIngestionResponse
{
    [JsonPropertyName("executionId")]
    public required string ExecutionId { get; init; }

    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("status")]
    public required WorkflowStatus Status { get; init; }
}

public sealed class SubmitIngestionDecisionRequest
{
    [JsonPropertyName("approved")]
    public bool Approved { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

// ---- Shared: Enums ----

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowStatus
{
    Pending,
    Running,
    AwaitingHumanApproval,
    Completed,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IngestionStage
{
    Pending,
    IngestionTranslation,
    MetadataLinking,
    HumanApproval,
    Completed,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryStage
{
    Pending,
    ChatActive,
    CurationRunning,
    AwaitingComplianceReview,
    Completed,
    Failed
}

// ---- Shared: Supporting Types ----

public sealed class StudySummaryResponse
{
    [JsonPropertyName("studyId")]
    public string StudyId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("compound")]
    public string Compound { get; init; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = string.Empty;

    [JsonPropertyName("primaryEndpoint")]
    public string PrimaryEndpoint { get; init; } = string.Empty;

    [JsonPropertyName("sourceSystems")]
    public IReadOnlyList<string> SourceSystems { get; init; } = [];
}

public sealed class PortfolioScenarioResponse
{
    [JsonPropertyName("scenarioId")]
    public string ScenarioId { get; init; } = string.Empty;

    [JsonPropertyName("block")]
    public string Block { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("studyId")]
    public string StudyId { get; init; } = string.Empty;

    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    [JsonPropertyName("sampleQuestion")]
    public string? SampleQuestion { get; init; }

    [JsonPropertyName("outcomeHint")]
    public string OutcomeHint { get; init; } = string.Empty;

    [JsonPropertyName("study")]
    public required StudySummaryResponse Study { get; init; }

    [JsonPropertyName("legacyScenarioId")]
    public string? LegacyScenarioId { get; init; }

    [JsonPropertyName("caseFolder")]
    public string? CaseFolder { get; init; }

    [JsonPropertyName("finalOutcome")]
    public string? FinalOutcome { get; init; }
}

public sealed class IngestionTranslationResultResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("documentsProcessed")]
    public int? DocumentsProcessed { get; init; }

    [JsonPropertyName("duplicatesRemoved")]
    public int? DuplicatesRemoved { get; init; }

    [JsonPropertyName("normalizedFormats")]
    public IReadOnlyList<string>? NormalizedFormats { get; init; }

    [JsonPropertyName("connectedPortals")]
    public IReadOnlyList<string>? ConnectedPortals { get; init; }
}

public sealed class MetadataLinkingResultResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("entities")]
    public IReadOnlyList<EntityChipResponse>? Entities { get; init; }

    [JsonPropertyName("links")]
    public IReadOnlyList<DocumentLinkResponse>? Links { get; init; }

    [JsonPropertyName("vectorsIndexed")]
    public int VectorsIndexed { get; init; }
}

public sealed class EntityChipResponse
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}

public sealed class DocumentLinkResponse
{
    [JsonPropertyName("fromDocument")]
    public string FromDocument { get; init; } = string.Empty;

    [JsonPropertyName("toTarget")]
    public string ToTarget { get; init; } = string.Empty;

    [JsonPropertyName("relationship")]
    public string Relationship { get; init; } = string.Empty;
}

public sealed class RetrievalTraceEventResponse
{
    [JsonPropertyName("stage")]
    public string Stage { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class HumanDecisionRecordResponse
{
    [JsonPropertyName("approved")]
    public bool Approved { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("decidedAt")]
    public DateTimeOffset DecidedAt { get; init; }
}

// ---- Block 1: Legacy (kept for backward compat) ----

public sealed class IngestionWorkflowStatusResponse
{
    public required string ExecutionId { get; init; }

    public required string SourceId { get; init; }

    public required string Status { get; init; }

    public required IngestionAgentOutputsResponse AgentOutputs { get; init; }

    public string? FailureReason { get; init; }

    public required DateTimeOffset LastUpdatedUtc { get; init; }
}

public sealed class IngestionAgentOutputsResponse
{
    public string? IngestionTranslation { get; init; }

    public string? MetadataLinking { get; init; }
}

// ---- Block 2: Query (Search & Chat + Curate) ----

public sealed class StartQueryWorkflowRequest
{
    [JsonPropertyName("executionId")]
    public string? ExecutionId { get; init; }
}

public sealed class AskRequest
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }
}

public sealed class ChatAnswerResponse
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }

    [JsonPropertyName("answer")]
    public required string Answer { get; init; }

    [JsonPropertyName("citations")]
    public required IReadOnlyList<string> Citations { get; init; }

    [JsonPropertyName("turnCount")]
    public required int TurnCount { get; init; }

    [JsonPropertyName("curateEnabled")]
    public required bool CurateEnabled { get; init; }
}

public sealed class StartCurateRequest
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("executionId")]
    public string? ExecutionId { get; init; }
}

public sealed class CurateWorkflowStatusResponse
{
    [JsonPropertyName("executionId")]
    public required string ExecutionId { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("curationOutput")]
    public string? CurationOutput { get; init; }

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; init; }

    [JsonPropertyName("lastUpdatedUtc")]
    public required DateTimeOffset LastUpdatedUtc { get; init; }
}

// ---- Block 2: New response types for frontend wire format ----

public sealed class QuerySessionStateResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("studyScope")]
    public string? StudyScope { get; init; }

    [JsonPropertyName("messages")]
    public IReadOnlyList<ChatMessageResponse> Messages { get; init; } = [];

    [JsonPropertyName("isChatRunning")]
    public bool IsChatRunning { get; init; }

    [JsonPropertyName("curationExecutionId")]
    public string? CurationExecutionId { get; init; }

    [JsonPropertyName("curationStatus")]
    public WorkflowStatus CurationStatus { get; init; }

    [JsonPropertyName("currentStage")]
    public string CurrentStage { get; init; } = "Pending";

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }

    [JsonPropertyName("curationCompliance")]
    public CurationComplianceResultResponse? CurationCompliance { get; init; }

    [JsonPropertyName("humanDecision")]
    public HumanDecisionRecordResponse? HumanDecision { get; init; }

    [JsonPropertyName("allowedActions")]
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
}

public sealed class ChatMessageResponse
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("citations")]
    public IReadOnlyList<CitationResponse>? Citations { get; init; }

    [JsonPropertyName("lineageSummary")]
    public string? LineageSummary { get; init; }

    [JsonPropertyName("retrievalTrace")]
    public IReadOnlyList<RetrievalTraceEventResponse>? RetrievalTrace { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class CitationResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("excerpt")]
    public string Excerpt { get; init; } = string.Empty;

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; init; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; init; }
}

public sealed class StartQueryWorkflowResponse
{
    [JsonPropertyName("executionId")]
    public string ExecutionId { get; init; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public WorkflowStatus Status { get; init; }
}

public sealed class StartCurationResponse
{
    [JsonPropertyName("curationExecutionId")]
    public string CurationExecutionId { get; init; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public WorkflowStatus Status { get; init; }
}

public sealed class SendChatMessageRequest
{
    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    [JsonPropertyName("studyScope")]
    public string? StudyScope { get; init; }
}

public sealed class SubmitQueryDecisionRequest
{
    [JsonPropertyName("approved")]
    public bool Approved { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

public sealed class SubmitQueryDecisionResponse
{
    [JsonPropertyName("executionId")]
    public string ExecutionId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public WorkflowStatus Status { get; init; }
}

public sealed class CurationWorkflowProgressResponse
{
    [JsonPropertyName("executionId")]
    public string ExecutionId { get; init; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public WorkflowStatus Status { get; init; }

    [JsonPropertyName("currentStage")]
    public QueryStage CurrentStage { get; init; }

    [JsonPropertyName("statusMessage")]
    public string StatusMessage { get; init; } = string.Empty;

    [JsonPropertyName("curationCompliance")]
    public CurationComplianceResultResponse? CurationCompliance { get; init; }

    [JsonPropertyName("humanDecision")]
    public HumanDecisionRecordResponse? HumanDecision { get; init; }

    [JsonPropertyName("allowedActions")]
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
}

public sealed class CurationComplianceResultResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("flags")]
    public IReadOnlyList<ComplianceFlagResponse>? Flags { get; init; }

    [JsonPropertyName("promptedOwners")]
    public IReadOnlyList<string>? PromptedOwners { get; init; }
}

public sealed class ComplianceFlagResponse
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("policyReference")]
    public string? PolicyReference { get; init; }
}

// ---- Block 1: Study Documents ----

public sealed class StudyDocumentsResponse
{
    [JsonPropertyName("studyId")]
    public string StudyId { get; init; } = string.Empty;

    [JsonPropertyName("sources")]
    public IReadOnlyList<KnowledgeSourceResponse> Sources { get; init; } = [];
}

public sealed class KnowledgeSourceResponse
{
    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("sourceType")]
    public string SourceType { get; init; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;
}

// ---- Vector DB Summary ----

public sealed class VectorDbStoreSummaryResponse
{
    [JsonPropertyName("totalStudies")]
    public int TotalStudies { get; init; }

    [JsonPropertyName("totalDocuments")]
    public int TotalDocuments { get; init; }

    [JsonPropertyName("totalEntities")]
    public int TotalEntities { get; init; }

    [JsonPropertyName("totalLinks")]
    public int TotalLinks { get; init; }

    [JsonPropertyName("lastIngestionAt")]
    public DateTimeOffset? LastIngestionAt { get; init; }

    [JsonPropertyName("lastIngestedStudyId")]
    public string? LastIngestedStudyId { get; init; }
}

// ---- Shared ----

public sealed class WorkflowApprovalRequest
{
    public required bool Approved { get; init; }

    public string? ReviewerComment { get; init; }
}

public sealed class ProblemDetailsResponse
{
    public required string Title { get; init; }

    public required string Detail { get; init; }
}
