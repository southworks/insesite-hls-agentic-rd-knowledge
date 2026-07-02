using System.Collections.Concurrent;
using CohereRndKnowledgeMining.Api.Host.Contracts;
using Microsoft.Extensions.AI;

namespace CohereRndKnowledgeMining.Api.Host.Services;

/// <summary>
/// A single Search &amp; Chat turn within a query session (Block 2, Process 1).
/// </summary>
public sealed class ChatTurn
{
    public required string Question { get; init; }

    public required string Answer { get; init; }

    public IReadOnlyList<string> Citations { get; init; } = [];

    /// <summary>True when this turn contains grounded knowledge eligible for Curate.</summary>
    public bool IsGrounded { get; init; }

    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// An interactive Search &amp; Chat session. Holds the conversation thread and the accumulated
/// responses that feed the on-demand Curate process (Block 2, Process 2).
/// </summary>
public sealed class QueryChatSession
{
    public required string SessionId { get; init; }

    /// <summary>Full conversation history maintained across turns (this AIAgent build is thread-less).</summary>
    public List<ChatMessage> History { get; } = [];

    public List<ChatTurn> Turns { get; } = [];

    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Frontend-facing query workflow execution binding a session id to an execution id.
/// </summary>
public sealed class QueryExecution
{
    public required string ExecutionId { get; init; }

    public required string SessionId { get; init; }

    public string? StudyScope { get; set; }

    public bool IsChatRunning { get; set; }

    public bool CurationStarted { get; set; }

    public HumanDecisionRecordDto? HumanDecision { get; set; }
}

public sealed class InMemoryQuerySessionStore
{
    private readonly ConcurrentDictionary<string, QueryChatSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QueryExecution> _queryExecutions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WorkflowExecution> _curateExecutions = new(StringComparer.OrdinalIgnoreCase);

    public QueryChatSession GetOrCreateSession(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, static id => new QueryChatSession { SessionId = id });
    }

    public QueryChatSession GetRequiredSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out QueryChatSession? session))
        {
            return session;
        }

        throw new KeyNotFoundException($"Query session '{sessionId}' was not found.");
    }

    public void SaveQueryExecution(QueryExecution execution) =>
        _queryExecutions[execution.ExecutionId] = execution;

    public QueryExecution GetRequiredQueryExecution(string executionId)
    {
        if (_queryExecutions.TryGetValue(executionId, out QueryExecution? execution))
        {
            return execution;
        }

        throw new KeyNotFoundException($"Query execution '{executionId}' was not found.");
    }

    public bool TryGetQueryExecution(string executionId, out QueryExecution? execution) =>
        _queryExecutions.TryGetValue(executionId, out execution);

    public void SaveCurateExecution(WorkflowExecution execution) => _curateExecutions[execution.ExecutionId] = execution;

    public WorkflowExecution GetRequiredCurateExecution(string executionId)
    {
        if (_curateExecutions.TryGetValue(executionId, out WorkflowExecution? execution))
        {
            return execution;
        }

        throw new KeyNotFoundException($"Curate execution '{executionId}' was not found.");
    }

    public bool TryGetCurateExecution(string executionId, out WorkflowExecution? execution) =>
        _curateExecutions.TryGetValue(executionId, out execution);
}
