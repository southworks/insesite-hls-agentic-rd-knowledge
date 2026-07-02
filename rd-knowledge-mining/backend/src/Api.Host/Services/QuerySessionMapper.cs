using CohereRndKnowledgeMining.Api.Host.Contracts;
using CohereRndKnowledgeMining.Api.Host.Workflow;

namespace CohereRndKnowledgeMining.Api.Host.Services;

internal static class QuerySessionMapper
{
    private const string CurationComplianceKey = "CurationCompliance";

    public static QuerySessionStateDto ToSessionState(
        QueryExecution execution,
        QueryChatSession session,
        WorkflowExecution? curateExecution)
    {
        WorkflowStatus curationStatus = ResolveCurationStatus(execution, curateExecution);
        QueryStage stage = ResolveStage(session, curateExecution);
        CurationComplianceResultDto? curation = ParseCurationResult(curateExecution);
        HumanDecisionRecordDto? humanDecision = execution.HumanDecision;
        var allowedActions = new List<string>();

        if (curationStatus == WorkflowStatus.AwaitingHumanApproval)
        {
            allowedActions.Add("SubmitDecision");
        }

        return new QuerySessionStateDto
        {
            SessionId = session.SessionId,
            StudyScope = execution.StudyScope,
            Messages = BuildMessages(session),
            IsChatRunning = execution.IsChatRunning,
            CurationExecutionId = execution.CurationStarted ? execution.ExecutionId : null,
            CurationStatus = curationStatus,
            CurrentStage = stage,
            StatusMessage = BuildSessionStatusMessage(execution, session, curateExecution, stage),
            CurationCompliance = curation,
            HumanDecision = humanDecision,
            AllowedActions = allowedActions
        };
    }

    public static CurationWorkflowProgressDto ToCurationProgress(
        QueryExecution execution,
        WorkflowExecution curateExecution)
    {
        QueryStage stage = curateExecution.Status switch
        {
            WorkflowStatus.Running => QueryStage.CurationRunning,
            WorkflowStatus.AwaitingHumanApproval => QueryStage.AwaitingComplianceReview,
            WorkflowStatus.Completed => QueryStage.Completed,
            WorkflowStatus.Failed => QueryStage.Failed,
            _ => QueryStage.Pending
        };

        var allowedActions = new List<string>();
        if (curateExecution.Status == WorkflowStatus.AwaitingHumanApproval)
        {
            allowedActions.Add("SubmitDecision");
        }

        return new CurationWorkflowProgressDto
        {
            ExecutionId = execution.ExecutionId,
            SessionId = execution.SessionId,
            Status = curateExecution.Status,
            CurrentStage = stage,
            StatusMessage = BuildCurationStatusMessage(curateExecution, stage),
            CurationCompliance = ParseCurationResult(curateExecution),
            HumanDecision = execution.HumanDecision,
            AllowedActions = allowedActions
        };
    }

    private static WorkflowStatus ResolveCurationStatus(QueryExecution execution, WorkflowExecution? curateExecution)
    {
        if (!execution.CurationStarted || curateExecution is null)
        {
            return WorkflowStatus.Pending;
        }

        return curateExecution.Status;
    }

    private static QueryStage ResolveStage(QueryChatSession session, WorkflowExecution? curateExecution)
    {
        if (curateExecution is not null)
        {
            return curateExecution.Status switch
            {
                WorkflowStatus.Running => QueryStage.CurationRunning,
                WorkflowStatus.AwaitingHumanApproval => QueryStage.AwaitingComplianceReview,
                WorkflowStatus.Completed => QueryStage.Completed,
                WorkflowStatus.Failed => QueryStage.Failed,
                _ => QueryStage.Pending
            };
        }

        return session.Turns.Count > 0 ? QueryStage.ChatActive : QueryStage.Pending;
    }

    private static IReadOnlyList<ChatMessageDto> BuildMessages(QueryChatSession session)
    {
        var messages = new List<ChatMessageDto>();

        foreach (ChatTurn turn in session.Turns)
        {
            messages.Add(new ChatMessageDto
            {
                Role = "user",
                Content = turn.Question,
                Citations = null,
                LineageSummary = null,
                RetrievalTrace = null,
                Timestamp = turn.CreatedUtc
            });

            AgentStructuredOutput? structured = AgentStructuredOutputParser.TryParseStructuredOutput(turn.Answer);
            messages.Add(new ChatMessageDto
            {
                Role = "assistant",
                Content = turn.Answer,
                Citations = MapCitations(turn.Citations),
                LineageSummary = structured?.Lineage,
                RetrievalTrace = turn.IsGrounded
                    ?
                    [
                        new RetrievalTraceEventDto
                        {
                            Stage = "Vector DB retrieval",
                            Description = "Top-N passages retrieved and reranked for grounding.",
                            ItemCount = turn.Citations.Count,
                            Timestamp = turn.CreatedUtc
                        }
                    ]
                    : null,
                Timestamp = turn.CreatedUtc
            });
        }

        return messages;
    }

    private static IReadOnlyList<CitationDto>? MapCitations(IReadOnlyList<string> citations)
    {
        if (citations.Count == 0)
        {
            return null;
        }

        return citations
            .Select(citation => new CitationDto
            {
                DocumentId = citation,
                Title = citation,
                Excerpt = string.Empty,
                SourceSystem = "Vector DB",
                RelevanceScore = 1.0
            })
            .ToArray();
    }

    private static CurationComplianceResultDto? ParseCurationResult(WorkflowExecution? curateExecution)
    {
        if (curateExecution is null)
        {
            return null;
        }

        if (!curateExecution.AgentOutputs.TryGetValue(CurationComplianceKey, out string? rawOutput)
            || string.IsNullOrWhiteSpace(rawOutput))
        {
            return null;
        }

        AgentStructuredOutput? structured = AgentStructuredOutputParser.TryParseStructuredOutput(rawOutput);
        if (structured is null)
        {
            return new CurationComplianceResultDto
            {
                Summary = rawOutput.Trim(),
                Flags = [],
                PromptedOwners = []
            };
        }

        var flags = (structured.Flags ?? [])
            .Select(flag => new ComplianceFlagDto
            {
                Severity = InferSeverity(flag),
                Category = "Compliance",
                Description = flag,
                PolicyReference = structured.PolicyRefs?.FirstOrDefault()
            })
            .ToArray();

        var promptedOwners = (structured.CapturedDecisions ?? [])
            .Concat(structured.PolicyRefs ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CurationComplianceResultDto
        {
            Summary = structured.Summary,
            Flags = flags,
            PromptedOwners = promptedOwners
        };
    }

    private static string InferSeverity(string flag)
    {
        if (flag.Contains("high", StringComparison.OrdinalIgnoreCase)
            || flag.Contains("sensitive", StringComparison.OrdinalIgnoreCase)
            || flag.Contains("phi", StringComparison.OrdinalIgnoreCase))
        {
            return "High";
        }

        if (flag.Contains("medium", StringComparison.OrdinalIgnoreCase)
            || flag.Contains("gap", StringComparison.OrdinalIgnoreCase))
        {
            return "Medium";
        }

        return "Low";
    }

    private static string BuildSessionStatusMessage(
        QueryExecution execution,
        QueryChatSession session,
        WorkflowExecution? curateExecution,
        QueryStage stage)
    {
        if (execution.IsChatRunning)
        {
            return "Search & Chat retrieving grounded evidence from Vector DB…";
        }

        return stage switch
        {
            QueryStage.ChatActive =>
                "Search & Chat active — ask follow-up questions or click Curate when ready.",
            QueryStage.CurationRunning =>
                "Curation & Compliance reviewing accumulated chat responses…",
            QueryStage.AwaitingComplianceReview =>
                "Compliance Reviewer: review curation flags and captured decisions.",
            QueryStage.Completed => "Curation cycle approved and audited.",
            QueryStage.Failed => curateExecution?.FailureReason ?? "Curation cycle denied or failed.",
            _ => session.Turns.Count > 0
                ? "Search & Chat active — ask follow-up questions or click Curate when ready."
                : "Ask a research question to begin Search & Chat."
        };
    }

    private static string BuildCurationStatusMessage(WorkflowExecution curateExecution, QueryStage stage) =>
        stage switch
        {
            QueryStage.CurationRunning => "Curation & Compliance reviewing accumulated chat responses…",
            QueryStage.AwaitingComplianceReview =>
                "Compliance Reviewer: review curation flags and captured decisions.",
            QueryStage.Completed => "Curation cycle approved and audited.",
            QueryStage.Failed => curateExecution.FailureReason ?? "Curation cycle denied or failed.",
            _ => "Preparing curation workflow…"
        };
}
