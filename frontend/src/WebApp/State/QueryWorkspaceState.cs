using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;
using Cohere.AgenticRDKnowledge.WebApp.Configuration;
using Cohere.AgenticRDKnowledge.WebApp.Services;
using Microsoft.Extensions.Options;

namespace Cohere.AgenticRDKnowledge.WebApp.State;

public sealed class QueryWorkspaceState : IAsyncDisposable
{
    private readonly IRdKnowledgeApiClient _apiClient;
    private readonly KnowledgeSessionStore _sessionStore;
    private readonly WorkflowPollingOptions _pollingOptions;
    private CancellationTokenSource? _pollingCts;

    public string? SessionId { get; private set; }
    public string? ExecutionId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string? StudyScope { get; private set; }
    public QueryWorkflowProgress? Progress { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsPolling { get; private set; }
    public string? PollingStatusMessage { get; private set; }
    public string? Error { get; private set; }

    public bool CanStartWorkflow =>
        Progress is null || Progress.Status is WorkflowStatus.Pending or WorkflowStatus.Failed;

    public bool CanSubmitDecision =>
        Progress?.Status == WorkflowStatus.AwaitingHumanApproval;

    public event Action? OnChange;

    public QueryWorkspaceState(
        IRdKnowledgeApiClient apiClient,
        KnowledgeSessionStore sessionStore,
        IOptions<WorkflowPollingOptions> pollingOptions)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _pollingOptions = pollingOptions.Value;
    }

    public async Task LoadAsync(string sessionId, string? executionId, CancellationToken cancellationToken = default)
    {
        SessionId = sessionId;
        ExecutionId = executionId;
        IsBusy = true;
        Error = null;
        Notify();

        try
        {
            var session = _sessionStore.GetSession(sessionId);
            Question = session?.SampleQuestion ?? string.Empty;
            StudyScope = session?.StudyId;

            if (!string.IsNullOrWhiteSpace(executionId))
            {
                Progress = await _apiClient.GetQueryStatusAsync(executionId, cancellationToken);
                Question = Progress.Question;
                StudyScope = Progress.StudyScope;
                UpdateSession();
                if (ShouldPoll(Progress.Status))
                {
                    StartPolling();
                }
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    public async Task StartWorkflowAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SessionId) || string.IsNullOrWhiteSpace(Question))
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Notify();

        try
        {
            var response = await _apiClient.StartQueryWorkflowAsync(
                new StartQueryWorkflowRequest(SessionId, Question, StudyScope),
                cancellationToken);
            ExecutionId = response.ExecutionId;
            Progress = await _apiClient.GetQueryStatusAsync(response.ExecutionId, cancellationToken);
            UpdateSession();
            StartPolling();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    public async Task SubmitDecisionAsync(bool approved, string? notes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ExecutionId))
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Notify();

        try
        {
            await _apiClient.SubmitQueryDecisionAsync(
                ExecutionId,
                new SubmitQueryDecisionRequest(approved, notes),
                cancellationToken);
            Progress = await _apiClient.GetQueryStatusAsync(ExecutionId, cancellationToken);
            UpdateSession();
            StopPolling();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    public void SetQuestion(string question)
    {
        Question = question;
        Notify();
    }

    private void StartPolling()
    {
        StopPolling();
        _pollingCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollingCts.Token);
    }

    private void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
        IsPolling = false;
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        IsPolling = true;
        var started = DateTimeOffset.UtcNow;
        var maxDuration = TimeSpan.FromMinutes(_pollingOptions.MaxDurationMinutes);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow - started > maxDuration)
                {
                    PollingStatusMessage = "Polling stopped: maximum duration exceeded.";
                    break;
                }

                if (string.IsNullOrWhiteSpace(ExecutionId))
                {
                    break;
                }

                Progress = await _apiClient.GetQueryStatusAsync(ExecutionId, cancellationToken);
                UpdateSession();
                PollingStatusMessage = Progress.StatusMessage;
                Notify();

                if (!ShouldPoll(Progress.Status))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(_pollingOptions.IntervalSeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose.
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Notify();
        }
        finally
        {
            IsPolling = false;
            Notify();
        }
    }

    private static bool ShouldPoll(WorkflowStatus status) =>
        status is WorkflowStatus.Running or WorkflowStatus.Pending;

    private void UpdateSession()
    {
        if (SessionId is null || Progress is null)
        {
            return;
        }

        var session = _sessionStore.GetSession(SessionId);
        if (session is null)
        {
            return;
        }

        session.ExecutionId = ExecutionId;
        session.Status = Progress.Status;
        _sessionStore.UpdateSession(session);
    }

    private void Notify() => OnChange?.Invoke();

    public ValueTask DisposeAsync()
    {
        StopPolling();
        return ValueTask.CompletedTask;
    }
}
