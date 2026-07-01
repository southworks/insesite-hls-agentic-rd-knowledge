namespace CohereRndKnowledgeMining.Api.Host.Contracts;

// ---- Block 1: Ingestion ----

public sealed class StartIngestionRequest
{
    /// <summary>Identifier of the raw R&D knowledge batch to ingest.</summary>
    public required string SourceId { get; init; }

    /// <summary>Optional caller-provided execution id; generated when omitted.</summary>
    public string? ExecutionId { get; init; }
}

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

public sealed class AskRequest
{
    public required string SessionId { get; init; }

    public required string Question { get; init; }
}

public sealed class ChatAnswerResponse
{
    public required string SessionId { get; init; }

    public required string Question { get; init; }

    public required string Answer { get; init; }

    public required IReadOnlyList<string> Citations { get; init; }

    public required int TurnCount { get; init; }

    /// <summary>True when the session has at least one grounded Search &amp; Chat response and Curate may be started.</summary>
    public required bool CurateEnabled { get; init; }
}

public sealed class StartCurateRequest
{
    public required string SessionId { get; init; }

    public string? ExecutionId { get; init; }
}

public sealed class CurateWorkflowStatusResponse
{
    public required string ExecutionId { get; init; }

    public required string SessionId { get; init; }

    public required string Status { get; init; }

    public string? CurationOutput { get; init; }

    public string? FailureReason { get; init; }

    public required DateTimeOffset LastUpdatedUtc { get; init; }
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
