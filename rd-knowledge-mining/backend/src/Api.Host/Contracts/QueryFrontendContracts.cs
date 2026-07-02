namespace CohereRndKnowledgeMining.Api.Host.Contracts;

using CohereRndKnowledgeMining.Api.Host.Services;

public enum QueryStage
{
    Pending,
    ChatActive,
    CurationRunning,
    AwaitingComplianceReview,
    Completed,
    Failed
}

public sealed class CitationDto
{
    public required string DocumentId { get; init; }

    public required string Title { get; init; }

    public required string Excerpt { get; init; }

    public required string SourceSystem { get; init; }

    public double RelevanceScore { get; init; }
}

public sealed class ChatMessageDto
{
    public required string Role { get; init; }

    public required string Content { get; init; }

    public IReadOnlyList<CitationDto>? Citations { get; init; }

    public string? LineageSummary { get; init; }

    public IReadOnlyList<RetrievalTraceEventDto>? RetrievalTrace { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}

public sealed class RetrievalTraceEventDto
{
    public required string Stage { get; init; }

    public required string Description { get; init; }

    public int ItemCount { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}

public sealed class ComplianceFlagDto
{
    public required string Severity { get; init; }

    public required string Category { get; init; }

    public required string Description { get; init; }

    public string? PolicyReference { get; init; }
}

public sealed class CurationComplianceResultDto
{
    public required string Summary { get; init; }

    public required IReadOnlyList<ComplianceFlagDto> Flags { get; init; }

    public required IReadOnlyList<string> PromptedOwners { get; init; }
}

public sealed class HumanDecisionRecordDto
{
    public required bool Approved { get; init; }

    public string? Notes { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

public sealed class StartQueryWorkflowResponseDto
{
    public required string ExecutionId { get; init; }

    public required string SessionId { get; init; }

    public required WorkflowStatus Status { get; init; }
}

public sealed class QuerySessionStateDto
{
    public required string SessionId { get; init; }

    public string? StudyScope { get; init; }

    public required IReadOnlyList<ChatMessageDto> Messages { get; init; }

    public required bool IsChatRunning { get; init; }

    public string? CurationExecutionId { get; init; }

    public required WorkflowStatus CurationStatus { get; init; }

    public required QueryStage CurrentStage { get; init; }

    public required string StatusMessage { get; init; }

    public CurationComplianceResultDto? CurationCompliance { get; init; }

    public HumanDecisionRecordDto? HumanDecision { get; init; }

    public required IReadOnlyList<string> AllowedActions { get; init; }
}

public sealed class SendChatMessageRequestDto
{
    public required string Question { get; init; }

    public string? StudyScope { get; init; }
}

public sealed class StartCurationResponseDto
{
    public required string CurationExecutionId { get; init; }

    public required string SessionId { get; init; }

    public required WorkflowStatus Status { get; init; }
}

public sealed class CurationWorkflowProgressDto
{
    public required string ExecutionId { get; init; }

    public required string SessionId { get; init; }

    public required WorkflowStatus Status { get; init; }

    public required QueryStage CurrentStage { get; init; }

    public required string StatusMessage { get; init; }

    public CurationComplianceResultDto? CurationCompliance { get; init; }

    public HumanDecisionRecordDto? HumanDecision { get; init; }

    public required IReadOnlyList<string> AllowedActions { get; init; }
}

public sealed class SubmitQueryDecisionRequestDto
{
    public required bool Approved { get; init; }

    public string? Notes { get; init; }
}

public sealed class SubmitQueryDecisionResponseDto
{
    public required string ExecutionId { get; init; }

    public required WorkflowStatus Status { get; init; }
}

public sealed class VectorDbStoreSummaryDto
{
    public int TotalStudies { get; init; }

    public int TotalDocuments { get; init; }

    public int TotalEntities { get; init; }

    public int TotalLinks { get; init; }

    public DateTimeOffset? LastIngestionAt { get; init; }

    public string? LastIngestedStudyId { get; init; }
}
