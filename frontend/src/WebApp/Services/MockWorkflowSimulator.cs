using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;
using Cohere.AgenticRDKnowledge.Shared.Contracts.VectorDb;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public sealed class MockExecutionState
{
    public required string ExecutionId { get; init; }
    public required WorkflowBlock Block { get; init; }
    public required string ResourceId { get; init; }
    public string? StudyScope { get; init; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public int PollCount { get; set; }
    public HumanDecisionRecord? HumanDecision { get; set; }
    public bool VectorDbUpdated { get; set; }
}

public sealed class MockQuerySessionState
{
    public required string SessionId { get; init; }
    public string? StudyScope { get; set; }
    public List<ChatMessage> Messages { get; } = [];
    public bool IsChatRunning { get; set; }
    public int ChatPollCount { get; set; }
    public string? PendingQuestion { get; set; }
    public string? CurationExecutionId { get; set; }
}

public sealed class MockWorkflowSimulator
{
    private readonly Dictionary<string, MockExecutionState> _executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MockQuerySessionState> _querySessions = new(StringComparer.OrdinalIgnoreCase);
    private VectorDbStoreSummary _vectorDbSummary = new(0, 0, 0, 0, null, null);
    private readonly object _lock = new();

    public VectorDbStoreSummary GetVectorDbSummary()
    {
        lock (_lock)
        {
            return _vectorDbSummary;
        }
    }

    public MockExecutionState StartIngestion(string executionId, string studyId)
    {
        var state = new MockExecutionState
        {
            ExecutionId = executionId,
            Block = WorkflowBlock.Ingestion,
            ResourceId = studyId,
            Status = WorkflowStatus.Running
        };

        lock (_lock)
        {
            _executions[executionId] = state;
        }

        return state;
    }

    public MockQuerySessionState GetOrCreateQuerySession(string sessionId, string? studyScope = null)
    {
        lock (_lock)
        {
            if (!_querySessions.TryGetValue(sessionId, out var session))
            {
                session = new MockQuerySessionState { SessionId = sessionId, StudyScope = studyScope };
                _querySessions[sessionId] = session;
            }
            else if (studyScope is not null)
            {
                session.StudyScope = studyScope;
            }

            return session;
        }
    }

    public MockQuerySessionState? GetQuerySession(string sessionId)
    {
        lock (_lock)
        {
            return _querySessions.TryGetValue(sessionId, out var session) ? session : null;
        }
    }

    public void BeginChatTurn(string sessionId, string question, string? studyScope)
    {
        lock (_lock)
        {
            var session = GetOrCreateQuerySession(sessionId, studyScope);
            session.PendingQuestion = question;
            session.ChatPollCount = 0;
            session.IsChatRunning = true;
            session.Messages.Add(new ChatMessage(
                "user",
                question,
                null,
                null,
                null,
                DateTimeOffset.UtcNow));
        }
    }

    public void AdvanceChatOnPoll(string sessionId)
    {
        lock (_lock)
        {
            if (!_querySessions.TryGetValue(sessionId, out var session) || !session.IsChatRunning)
            {
                return;
            }

            session.ChatPollCount++;
            if (session.ChatPollCount < 2)
            {
                return;
            }

            session.IsChatRunning = false;
        }
    }

    public MockExecutionState StartCuration(string sessionId)
    {
        var executionId = $"cur-{Guid.NewGuid():N}"[..12];
        var state = new MockExecutionState
        {
            ExecutionId = executionId,
            Block = WorkflowBlock.Query,
            ResourceId = sessionId,
            Status = WorkflowStatus.Running
        };

        lock (_lock)
        {
            var session = GetOrCreateQuerySession(sessionId);
            session.CurationExecutionId = executionId;
            _executions[executionId] = state;
        }

        return state;
    }

    public MockExecutionState? GetExecution(string executionId)
    {
        lock (_lock)
        {
            return _executions.TryGetValue(executionId, out var state) ? state : null;
        }
    }

    public MockExecutionState AdvanceOnPoll(string executionId)
    {
        lock (_lock)
        {
            if (!_executions.TryGetValue(executionId, out var state))
            {
                throw new KeyNotFoundException($"Execution {executionId} not found.");
            }

            if (state.Status is WorkflowStatus.Completed or WorkflowStatus.Failed)
            {
                return state;
            }

            state.PollCount++;

            if (state.Block == WorkflowBlock.Ingestion)
            {
                state.Status = state.PollCount switch
                {
                    <= 2 => WorkflowStatus.Running,
                    <= 4 => WorkflowStatus.Running,
                    _ => WorkflowStatus.AwaitingHumanApproval
                };
            }
            else
            {
                state.Status = state.PollCount switch
                {
                    <= 2 => WorkflowStatus.Running,
                    _ => WorkflowStatus.AwaitingHumanApproval
                };
            }

            return state;
        }
    }

    public void SubmitDecision(string executionId, bool approved, string? notes)
    {
        lock (_lock)
        {
            if (!_executions.TryGetValue(executionId, out var state))
            {
                throw new KeyNotFoundException($"Execution {executionId} not found.");
            }

            state.HumanDecision = new HumanDecisionRecord(approved, notes, DateTimeOffset.UtcNow);
            state.Status = approved ? WorkflowStatus.Completed : WorkflowStatus.Failed;

            if (approved && state.Block == WorkflowBlock.Ingestion && !state.VectorDbUpdated)
            {
                _vectorDbSummary = _vectorDbSummary with
                {
                    TotalStudies = _vectorDbSummary.TotalStudies + 1,
                    TotalDocuments = _vectorDbSummary.TotalDocuments + 12,
                    TotalEntities = _vectorDbSummary.TotalEntities + 28,
                    TotalLinks = _vectorDbSummary.TotalLinks + 15,
                    LastIngestionAt = DateTimeOffset.UtcNow,
                    LastIngestedStudyId = state.ResourceId
                };
                state.VectorDbUpdated = true;
            }
        }
    }

    public IngestionStage GetIngestionStage(MockExecutionState state) => state.Status switch
    {
        WorkflowStatus.Pending => IngestionStage.Pending,
        WorkflowStatus.Running => state.PollCount <= 2
            ? IngestionStage.IngestionTranslation
            : IngestionStage.MetadataLinking,
        WorkflowStatus.AwaitingHumanApproval => IngestionStage.HumanApproval,
        WorkflowStatus.Completed => IngestionStage.Completed,
        WorkflowStatus.Failed => IngestionStage.Failed,
        _ => IngestionStage.Pending
    };

    public QueryStage GetCurationStage(MockExecutionState state) => state.Status switch
    {
        WorkflowStatus.Pending => QueryStage.Pending,
        WorkflowStatus.Running => QueryStage.CurationRunning,
        WorkflowStatus.AwaitingHumanApproval => QueryStage.AwaitingComplianceReview,
        WorkflowStatus.Completed => QueryStage.Completed,
        WorkflowStatus.Failed => QueryStage.Failed,
        _ => QueryStage.Pending
    };

    public QueryStage GetQuerySessionStage(MockQuerySessionState session)
    {
        if (session.CurationExecutionId is not null &&
            _executions.TryGetValue(session.CurationExecutionId, out var curation))
        {
            return GetCurationStage(curation);
        }

        if (session.IsChatRunning || session.Messages.Count > 0)
        {
            return QueryStage.ChatActive;
        }

        return QueryStage.Pending;
    }

    public IReadOnlyList<RetrievalTraceEvent> BuildRetrievalTrace(int pollCount, string blockLabel)
    {
        if (pollCount < 1)
        {
            return [];
        }

        var baseTime = DateTimeOffset.UtcNow.AddSeconds(-pollCount);
        return
        [
            new RetrievalTraceEvent("Cohere Embed", $"{blockLabel}: embedded candidate passages", 48, baseTime.AddSeconds(1)),
            new RetrievalTraceEvent("Vector DB", "Retrieved nearest neighbors from index", 48, baseTime.AddSeconds(2)),
            new RetrievalTraceEvent("Cohere Rerank", "Re-ordered candidates by relevance", 12, baseTime.AddSeconds(3)),
            new RetrievalTraceEvent("Top-N context", "Selected top passages for Command A+", 6, baseTime.AddSeconds(4))
        ];
    }
}
