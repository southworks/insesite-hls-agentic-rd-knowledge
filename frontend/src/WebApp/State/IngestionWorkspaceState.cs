using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;
using Cohere.AgenticRDKnowledge.WebApp.Configuration;
using Cohere.AgenticRDKnowledge.WebApp.Services;
using Microsoft.Extensions.Options;

namespace Cohere.AgenticRDKnowledge.WebApp.State;

public sealed class IngestionWorkspaceState : IAsyncDisposable
{
    private readonly IRdKnowledgeApiClient _apiClient;
    private readonly KnowledgeSessionStore _sessionStore;
    private readonly WorkflowPollingOptions _pollingOptions;
    private CancellationTokenSource? _pollingCts;

    public string? StudyId { get; private set; }
    public string? ExecutionId { get; private set; }
    public IngestionWorkflowProgress? Progress { get; private set; }
    public StudyDocumentsResponse? Documents { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsPolling { get; private set; }
    public string? PollingStatusMessage { get; private set; }
    public string? Error { get; private set; }

    public bool CanStartWorkflow =>
        Progress is null || Progress.Status is WorkflowStatus.Pending or WorkflowStatus.Failed;

    public bool CanSubmitDecision =>
        Progress?.Status == WorkflowStatus.AwaitingHumanApproval;

    public event Action? OnChange;

    public IngestionWorkspaceState(
        IRdKnowledgeApiClient apiClient,
        KnowledgeSessionStore sessionStore,
        IOptions<WorkflowPollingOptions> pollingOptions)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _pollingOptions = pollingOptions.Value;
    }

    public async Task LoadAsync(string studyId, string? executionId, CancellationToken cancellationToken = default)
    {
        StudyId = studyId;
        ExecutionId = executionId;
        IsBusy = true;
        Error = null;
        Notify();

        try
        {
            Documents = await _apiClient.GetStudyDocumentsAsync(studyId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(executionId))
            {
                Progress = await _apiClient.GetIngestionStatusAsync(executionId, cancellationToken);
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
        if (string.IsNullOrWhiteSpace(StudyId))
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Notify();

        try
        {
            var response = await _apiClient.StartIngestionWorkflowAsync(StudyId, cancellationToken);
            ExecutionId = response.ExecutionId;
            Progress = await _apiClient.GetIngestionStatusAsync(response.ExecutionId, cancellationToken);
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
            await _apiClient.SubmitIngestionDecisionAsync(
                ExecutionId,
                new SubmitIngestionDecisionRequest(approved, notes),
                cancellationToken);
            Progress = await _apiClient.GetIngestionStatusAsync(ExecutionId, cancellationToken);
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

                Progress = await _apiClient.GetIngestionStatusAsync(ExecutionId, cancellationToken);
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
        if (StudyId is null || Progress is null)
        {
            return;
        }

        var session = _sessionStore.GetSession(StudyId) ?? _sessionStore.OpenIngestionSession(StudyId, StudyId);
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
