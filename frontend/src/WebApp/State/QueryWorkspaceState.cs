using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
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
    private CancellationTokenSource? _chatPollingCts;
    private CancellationTokenSource? _curationPollingCts;

    public string? SessionId { get; private set; }
    public string? ExecutionId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string? StudyScope { get; private set; }
    public QuerySessionState? Session { get; private set; }
    public CurationWorkflowProgress? CurationProgress { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsChatPolling { get; private set; }
    public bool IsCurationPolling { get; private set; }
    public string? Error { get; private set; }

    public bool CanSendMessage =>
        !IsBusy && !IsChatPolling && Session?.IsChatRunning != true &&
        CurationProgress?.Status is not (WorkflowStatus.Running or WorkflowStatus.AwaitingHumanApproval);

    public bool CanStartCuration =>
        !IsBusy && Session?.Messages.Count > 0 && Session.IsChatRunning != true &&
        CurationProgress?.Status is not (WorkflowStatus.Running or WorkflowStatus.AwaitingHumanApproval or WorkflowStatus.Completed);

    public bool CanSubmitDecision =>
        CurationProgress?.Status == WorkflowStatus.AwaitingHumanApproval;

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
            var stored = executionId is not null
                ? _sessionStore.GetQuerySession(executionId)
                : _sessionStore.GetQuerySessionBySessionId(sessionId);
            Question = stored?.SampleQuestion ?? string.Empty;
            StudyScope = stored?.StudyId;

            if (!string.IsNullOrWhiteSpace(executionId))
            {
                Session = await _apiClient.GetQuerySessionAsync(executionId, cancellationToken);
                StudyScope = Session.StudyScope ?? StudyScope;
                UpdateSessionFromQueryState();

                if (Session.IsChatRunning)
                {
                    StartChatPolling();
                }

                if (Session.CurationExecutionId is not null)
                {
                    CurationProgress = await _apiClient.GetCurationStatusAsync(executionId, cancellationToken);
                    UpdateSessionFromCurationProgress();
                    if (ShouldPollCuration(CurationProgress.Status))
                    {
                        StartCurationPolling();
                    }
                }
                else
                {
                    StopCurationPolling();
                    CurationProgress = null;
                }
            }
            else
            {
                StopChatPolling();
                StopCurationPolling();
                Session = null;
                CurationProgress = null;
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

    public async Task SendMessageAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ExecutionId) || string.IsNullOrWhiteSpace(Question) || !CanSendMessage)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Notify();

        try
        {
            Session = await _apiClient.SendChatMessageAsync(
                ExecutionId,
                new SendChatMessageRequest(Question, StudyScope),
                cancellationToken);
            UpdateSessionFromQueryState();
            Question = string.Empty;
            StartChatPolling();
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

    public async Task StartCurationAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ExecutionId) || !CanStartCuration)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Notify();

        try
        {
            await _apiClient.StartCurationAsync(ExecutionId, cancellationToken);
            CurationProgress = await _apiClient.GetCurationStatusAsync(ExecutionId, cancellationToken);
            Session = await _apiClient.GetQuerySessionAsync(ExecutionId, cancellationToken);
            UpdateSessionFromCurationProgress();
            UpdateSessionFromQueryState();
            StartCurationPolling();
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
            await _apiClient.SubmitCurationDecisionAsync(
                ExecutionId,
                new SubmitQueryDecisionRequest(approved, notes),
                cancellationToken);
            CurationProgress = await _apiClient.GetCurationStatusAsync(ExecutionId, cancellationToken);
            Session = await _apiClient.GetQuerySessionAsync(ExecutionId, cancellationToken);
            UpdateSessionFromCurationProgress();
            UpdateSessionFromQueryState();
            StopCurationPolling();
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

    private void StartChatPolling()
    {
        StopChatPolling();
        _chatPollingCts = new CancellationTokenSource();
        _ = ChatPollLoopAsync(_chatPollingCts.Token);
    }

    private void StopChatPolling()
    {
        _chatPollingCts?.Cancel();
        _chatPollingCts?.Dispose();
        _chatPollingCts = null;
        IsChatPolling = false;
    }

    private void StartCurationPolling()
    {
        StopCurationPolling();
        _curationPollingCts = new CancellationTokenSource();
        _ = CurationPollLoopAsync(_curationPollingCts.Token);
    }

    private void StopCurationPolling()
    {
        _curationPollingCts?.Cancel();
        _curationPollingCts?.Dispose();
        _curationPollingCts = null;
        IsCurationPolling = false;
    }

    private async Task ChatPollLoopAsync(CancellationToken cancellationToken)
    {
        IsChatPolling = true;
        var started = DateTimeOffset.UtcNow;
        var maxDuration = TimeSpan.FromMinutes(_pollingOptions.MaxDurationMinutes);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow - started > maxDuration)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(ExecutionId))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(_pollingOptions.IntervalSeconds), cancellationToken);
                Session = await _apiClient.GetQuerySessionAsync(ExecutionId, cancellationToken);
                UpdateSessionFromQueryState();
                Notify();

                if (Session.IsChatRunning != true)
                {
                    break;
                }
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
            IsChatPolling = false;
            Notify();
        }
    }

    private async Task CurationPollLoopAsync(CancellationToken cancellationToken)
    {
        IsCurationPolling = true;
        var started = DateTimeOffset.UtcNow;
        var maxDuration = TimeSpan.FromMinutes(_pollingOptions.MaxDurationMinutes);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow - started > maxDuration)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(ExecutionId))
                {
                    break;
                }

                CurationProgress = await _apiClient.GetCurationStatusAsync(ExecutionId, cancellationToken);
                Session = await _apiClient.GetQuerySessionAsync(ExecutionId, cancellationToken);
                UpdateSessionFromCurationProgress();
                UpdateSessionFromQueryState();
                Notify();

                if (!ShouldPollCuration(CurationProgress.Status))
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
            IsCurationPolling = false;
            Notify();
        }
    }

    private static bool ShouldPollCuration(WorkflowStatus status) =>
        status is WorkflowStatus.Running or WorkflowStatus.Pending;

    private void UpdateSessionFromQueryState()
    {
        if (ExecutionId is null || Session is null)
        {
            return;
        }

        var session = _sessionStore.GetQuerySession(ExecutionId);
        if (session is null)
        {
            return;
        }

        session.ChatMessageCount = Session.Messages.Count;
        session.ExecutionId = ExecutionId;
        session.Status = CurationProgress?.Status ?? (Session.Messages.Count > 0 ? WorkflowStatus.Running : WorkflowStatus.Pending);
        _sessionStore.UpdateSession(session);
    }

    private void UpdateSessionFromCurationProgress()
    {
        if (ExecutionId is null || CurationProgress is null)
        {
            return;
        }

        var session = _sessionStore.GetQuerySession(ExecutionId);
        if (session is null)
        {
            return;
        }

        session.ExecutionId = ExecutionId;
        session.Status = CurationProgress.Status;
        _sessionStore.UpdateSession(session);
    }

    private void Notify() => OnChange?.Invoke();

    public ValueTask DisposeAsync()
    {
        StopChatPolling();
        StopCurationPolling();
        return ValueTask.CompletedTask;
    }
}
