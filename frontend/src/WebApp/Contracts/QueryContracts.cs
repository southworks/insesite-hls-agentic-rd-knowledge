using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;

namespace Cohere.AgenticRDKnowledge.Shared.Contracts.Query;

public sealed record StartQueryWorkflowRequest(
    string SessionId,
    string Question,
    string? StudyScope);

public sealed record StartQueryWorkflowResponse(
    string ExecutionId,
    string SessionId,
    WorkflowStatus Status);

public sealed record QueryWorkflowProgress(
    string ExecutionId,
    string SessionId,
    string Question,
    string? StudyScope,
    WorkflowStatus Status,
    QueryStage CurrentStage,
    string StatusMessage,
    SearchChatResult? SearchChat,
    CurationComplianceResult? CurationCompliance,
    IReadOnlyList<RetrievalTraceEvent>? RetrievalTrace,
    HumanDecisionRecord? HumanDecision,
    IReadOnlyList<string> AllowedActions);

public sealed record SubmitQueryDecisionRequest(
    bool Approved,
    string? Notes);

public sealed record SubmitQueryDecisionResponse(
    string ExecutionId,
    WorkflowStatus Status);
