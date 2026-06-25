using Cohere.AgenticRDKnowledge.Shared.Contracts;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public sealed record KnowledgeSessionSummary(
    string SessionId,
    string Title,
    WorkflowBlock Block,
    WorkflowStatus Status,
    string? ExecutionId,
    DateTimeOffset OpenedAt);

public sealed class KnowledgeSession
{
    public required string SessionId { get; init; }
    public required WorkflowBlock Block { get; init; }
    public required string Title { get; set; }
    public required string StudyId { get; init; }
    public string? ScenarioId { get; init; }
    public string? SampleQuestion { get; set; }
    public string? ExecutionId { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public DateTimeOffset OpenedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class KnowledgeSessionStore
{
    private readonly Dictionary<string, KnowledgeSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public KnowledgeSession OpenIngestionSession(string studyId, string title, string? scenarioId = null)
    {
        var session = new KnowledgeSession
        {
            SessionId = studyId,
            Block = WorkflowBlock.Ingestion,
            Title = title,
            StudyId = studyId,
            ScenarioId = scenarioId
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    public KnowledgeSession OpenQuerySession(string sessionId, string title, string studyScope, string question, string? scenarioId = null)
    {
        var session = new KnowledgeSession
        {
            SessionId = sessionId,
            Block = WorkflowBlock.Query,
            Title = title,
            StudyId = studyScope,
            ScenarioId = scenarioId,
            SampleQuestion = question
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    public KnowledgeSession? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public void UpdateSession(KnowledgeSession session) =>
        _sessions[session.SessionId] = session;

    public IReadOnlyList<KnowledgeSessionSummary> GetSummaries() =>
        _sessions.Values
            .OrderByDescending(s => s.OpenedAt)
            .Select(s => new KnowledgeSessionSummary(
                s.SessionId,
                s.Title,
                s.Block,
                s.Status,
                s.ExecutionId,
                s.OpenedAt))
            .ToList();
}
