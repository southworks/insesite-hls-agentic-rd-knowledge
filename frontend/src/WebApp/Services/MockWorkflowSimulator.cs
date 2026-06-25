using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Fabric;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public sealed class MockExecutionState
{
    public required string ExecutionId { get; init; }
    public required WorkflowBlock Block { get; init; }
    public required string ResourceId { get; init; }
    public string? Question { get; init; }
    public string? StudyScope { get; init; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public int PollCount { get; set; }
    public HumanDecisionRecord? HumanDecision { get; set; }
    public bool FabricUpdated { get; set; }
}

public sealed class MockWorkflowSimulator
{
    private readonly Dictionary<string, MockExecutionState> _executions = new(StringComparer.OrdinalIgnoreCase);
    private FabricStoreSummary _fabricSummary = new(0, 0, 0, 0, null, null);
    private readonly object _lock = new();

    public FabricStoreSummary GetFabricSummary()
    {
        lock (_lock)
        {
            return _fabricSummary;
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

    public MockExecutionState StartQuery(string executionId, string sessionId, string question, string? studyScope)
    {
        var state = new MockExecutionState
        {
            ExecutionId = executionId,
            Block = WorkflowBlock.Query,
            ResourceId = sessionId,
            Question = question,
            StudyScope = studyScope,
            Status = WorkflowStatus.Running
        };

        lock (_lock)
        {
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

            state.Status = state.Block switch
            {
                WorkflowBlock.Ingestion => state.PollCount switch
                {
                    <= 2 => WorkflowStatus.Running,
                    <= 4 => WorkflowStatus.Running,
                    _ => WorkflowStatus.AwaitingHumanApproval
                },
                WorkflowBlock.Query => state.PollCount switch
                {
                    <= 2 => WorkflowStatus.Running,
                    <= 4 => WorkflowStatus.Running,
                    _ => WorkflowStatus.AwaitingHumanApproval
                },
                _ => state.Status
            };

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

            if (approved && state.Block == WorkflowBlock.Ingestion && !state.FabricUpdated)
            {
                _fabricSummary = _fabricSummary with
                {
                    TotalStudies = _fabricSummary.TotalStudies + 1,
                    TotalDocuments = _fabricSummary.TotalDocuments + 12,
                    TotalEntities = _fabricSummary.TotalEntities + 28,
                    TotalLinks = _fabricSummary.TotalLinks + 15,
                    LastIngestionAt = DateTimeOffset.UtcNow,
                    LastIngestedStudyId = state.ResourceId
                };
                state.FabricUpdated = true;
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

    public QueryStage GetQueryStage(MockExecutionState state) => state.Status switch
    {
        WorkflowStatus.Pending => QueryStage.Pending,
        WorkflowStatus.Running => state.PollCount <= 2
            ? QueryStage.SearchChat
            : QueryStage.CurationCompliance,
        WorkflowStatus.AwaitingHumanApproval => QueryStage.HumanApproval,
        WorkflowStatus.Completed => QueryStage.Completed,
        WorkflowStatus.Failed => QueryStage.Failed,
        _ => QueryStage.Pending
    };

    public IReadOnlyList<RetrievalTraceEvent> BuildRetrievalTrace(MockExecutionState state, string blockLabel)
    {
        if (state.PollCount < 2)
        {
            return [];
        }

        var baseTime = DateTimeOffset.UtcNow.AddSeconds(-state.PollCount);
        return
        [
            new RetrievalTraceEvent("Cohere Embed", $"{blockLabel}: embedded candidate passages", 48, baseTime.AddSeconds(1)),
            new RetrievalTraceEvent("Vector DB", "Retrieved nearest neighbors from index", 48, baseTime.AddSeconds(2)),
            new RetrievalTraceEvent("Cohere Rerank", "Re-ordered candidates by relevance", 12, baseTime.AddSeconds(3)),
            new RetrievalTraceEvent("Top-N context", "Selected top passages for Command A+", 6, baseTime.AddSeconds(4))
        ];
    }
}
