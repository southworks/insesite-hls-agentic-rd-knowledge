using CohereRndKnowledgeMining.Api.Host.Contracts;
using CohereRndKnowledgeMining.Api.Host.Services.Integrations;
using CohereRndKnowledgeMining.Api.Host.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereRndKnowledgeMining.Api.Host.Services;

/// <summary>
/// Drives the Block 1 (Ingestion) workflow: reads raw knowledge from Fabric, runs the
/// ingestion-translation -> metadata-linking -> Knowledge Curator gate graph, and on approval
/// writes the curated knowledge to the Vector DB.
/// </summary>
public sealed class IngestionWorkflowService
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    private const string IngestionTranslationKey = "IngestionTranslation";
    private const string MetadataLinkingKey = "MetadataLinking";

    private readonly FoundryAgentProvider _agentProvider;
    private readonly IngestionWorkflowFactory _workflowFactory;
    private readonly InMemoryIngestionWorkflowStore _store;
    private readonly IFabricRawSourceReader _rawSourceReader;
    private readonly IVectorKnowledgeWriter _vectorKnowledgeWriter;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<IngestionWorkflowService> _logger;

    public IngestionWorkflowService(
        FoundryAgentProvider agentProvider,
        IngestionWorkflowFactory workflowFactory,
        InMemoryIngestionWorkflowStore store,
        IFabricRawSourceReader rawSourceReader,
        IVectorKnowledgeWriter vectorKnowledgeWriter,
        IHostApplicationLifetime applicationLifetime,
        ILogger<IngestionWorkflowService> logger)
    {
        _agentProvider = agentProvider;
        _workflowFactory = workflowFactory;
        _store = store;
        _rawSourceReader = rawSourceReader;
        _vectorKnowledgeWriter = vectorKnowledgeWriter;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    public async Task<IngestionWorkflowStatusResponse> StartIngestionAsync(
        string sourceId,
        string executionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new InvalidOperationException("SourceId is required.");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new InvalidOperationException("ExecutionId is required.");
        }

        IReadOnlyList<RawKnowledgeItem> items =
            await _rawSourceReader.ReadAsync(sourceId, cancellationToken).ConfigureAwait(false);

        if (items.Count == 0)
        {
            throw new KeyNotFoundException(
                $"Source '{sourceId}' has no raw R&D knowledge in Microsoft Fabric.");
        }

        var execution = new WorkflowExecution
        {
            ExecutionId = executionId,
            CorrelationId = sourceId.Trim(),
            Status = WorkflowStatus.Running,
            WorkflowCheckpointManager = CheckpointManager.CreateInMemory()
        };
        _store.Save(execution);

        List<ChatMessage> input = WorkflowPayloadBuilder.CreateInitialMessages(sourceId.Trim(), executionId, items);
        RunInBackground(executionId, input);

        return ToResponse(execution);
    }

    public IngestionWorkflowStatusResponse GetIngestionStatus(string executionId) =>
        ToResponse(_store.GetRequired(executionId));

    public IngestionWorkflowStatusResponse ResumeIngestionAsync(
        string sourceId,
        string executionId,
        bool approved,
        string? reviewerComment,
        CancellationToken cancellationToken)
    {
        WorkflowExecution execution = _store.GetRequired(executionId);

        if (!string.Equals(execution.CorrelationId, sourceId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Execution '{executionId}' does not belong to source '{sourceId}'.");
        }

        if (execution.Status != WorkflowStatus.AwaitingHumanApproval ||
            execution.PendingCheckpoint is null ||
            execution.PendingApprovalRequest is null ||
            execution.WorkflowCheckpointManager is null)
        {
            throw new InvalidOperationException(
                "Ingestion workflow is not waiting for human approval.");
        }

        execution.Status = WorkflowStatus.Running;
        execution.FailureReason = null;
        Touch(execution);

        ResumeInBackground(executionId, approved, reviewerComment);

        return ToResponse(execution);
    }

    private void RunInBackground(string executionId, IList<ChatMessage> input)
    {
        CancellationToken stopping = _applicationLifetime.ApplicationStopping;

        _ = Task.Run(async () =>
        {
            WorkflowExecution execution = _store.GetRequired(executionId);

            try
            {
                RndKnowledgeAgents agents = await _agentProvider.GetAgentsAsync(stopping).ConfigureAwait(false);
                AgentWorkflow workflow = _workflowFactory.CreateWorkflow(agents, execution.CorrelationId, executionId);

                await using StreamingRun run = await InProcessExecution
                    .RunStreamingAsync(
                        workflow,
                        input,
                        execution.WorkflowCheckpointManager ?? throw new InvalidOperationException("Checkpoint manager was not initialized."),
                        executionId,
                        stopping)
                    .ConfigureAwait(false);

                await RunUntilDoneAsync(execution, run, stopping, sendInitialTurnToken: true).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                MarkFailed(execution, "Workflow cancelled because the application is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ingestion workflow failed for execution {ExecutionId}.", executionId);
                MarkFailed(execution, ex.Message);
            }
        }, CancellationToken.None);
    }

    private void ResumeInBackground(string executionId, bool approved, string? reviewerComment)
    {
        CancellationToken stopping = _applicationLifetime.ApplicationStopping;

        _ = Task.Run(async () =>
        {
            WorkflowExecution execution = _store.GetRequired(executionId);

            try
            {
                RndKnowledgeAgents agents = await _agentProvider.GetAgentsAsync(stopping).ConfigureAwait(false);
                AgentWorkflow workflow = _workflowFactory.CreateWorkflow(agents, execution.CorrelationId, executionId);

                await using StreamingRun run = await InProcessExecution
                    .ResumeStreamingAsync(
                        workflow,
                        execution.PendingCheckpoint ?? throw new InvalidOperationException("Pending checkpoint was not initialized."),
                        execution.WorkflowCheckpointManager ?? throw new InvalidOperationException("Checkpoint manager was not initialized."),
                        stopping)
                    .ConfigureAwait(false);

                await ResumeRunAsync(execution, run, approved, reviewerComment, stopping).ConfigureAwait(false);

                if (execution.Status == WorkflowStatus.Completed)
                {
                    await PersistCuratedKnowledgeAsync(execution, stopping).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                MarkFailed(execution, "Workflow cancelled because the application is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ingestion workflow resume failed for execution {ExecutionId}.", executionId);
                MarkFailed(execution, ex.Message);
            }
        }, CancellationToken.None);
    }

    private async Task PersistCuratedKnowledgeAsync(WorkflowExecution execution, CancellationToken cancellationToken)
    {
        string curatedKnowledgeJson = GetOutput(execution, MetadataLinkingKey) ?? string.Empty;

        await _vectorKnowledgeWriter
            .WriteAsync(execution.CorrelationId, execution.ExecutionId, curatedKnowledgeJson, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Curated knowledge persisted to the Vector DB for source {SourceId}, execution {ExecutionId}.",
            execution.CorrelationId,
            execution.ExecutionId);
    }

    private async Task RunUntilDoneAsync(
        WorkflowExecution execution,
        StreamingRun run,
        CancellationToken cancellationToken,
        bool sendInitialTurnToken)
    {
        using CancellationTokenSource timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(RunTimeout);

        if (sendInitialTurnToken)
        {
            await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
        }

        while (!timeoutCts.IsCancellationRequested)
        {
            using CancellationTokenSource idleCts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

            idleCts.CancelAfter(IdleTimeout);

            try
            {
                await foreach (WorkflowEvent workflowEvent in run
                    .WatchStreamAsync(idleCts.Token)
                    .ConfigureAwait(false))
                {
                    HandleEvent(execution, workflowEvent);

                    if (execution.Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.AwaitingHumanApproval)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
                when (idleCts.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
            {
                // Idle timeout; query run status and continue driving the workflow.
            }

            if (execution.Status == WorkflowStatus.AwaitingHumanApproval)
            {
                return;
            }

            RunStatus status = await run.GetStatusAsync(timeoutCts.Token).ConfigureAwait(false);

            switch (status)
            {
                case RunStatus.Ended:
                    MarkCompleted(execution);
                    return;

                case RunStatus.PendingRequests:
                    if (execution.Status == WorkflowStatus.AwaitingHumanApproval)
                    {
                        return;
                    }

                    await SendTurnTokenAsync(run, timeoutCts.Token).ConfigureAwait(false);
                    break;

                case RunStatus.Running:
                case RunStatus.Idle:
                    break;

                default:
                    return;
            }
        }
    }

    private async Task ResumeRunAsync(
        WorkflowExecution execution,
        StreamingRun run,
        bool approved,
        string? reviewerComment,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(RunTimeout);

        bool responseSent = false;

        while (!timeoutCts.IsCancellationRequested)
        {
            using CancellationTokenSource idleCts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

            idleCts.CancelAfter(IdleTimeout);

            try
            {
                await foreach (WorkflowEvent workflowEvent in run
                    .WatchStreamAsync(idleCts.Token)
                    .ConfigureAwait(false))
                {
                    if (!responseSent &&
                        workflowEvent is RequestInfoEvent requestInfoEvent &&
                        requestInfoEvent.Request.TryGetDataAs(out HumanApprovalPrompt? _))
                    {
                        ExternalResponse response = requestInfoEvent.Request.CreateResponse(
                            new HumanApprovalDecision
                            {
                                Approved = approved,
                                ReviewerComment = reviewerComment
                            });

                        await run.SendResponseAsync(response).ConfigureAwait(false);
                        execution.PendingApprovalRequest = null;
                        execution.PendingCheckpoint = null;
                        execution.Status = WorkflowStatus.Running;
                        execution.FailureReason = null;
                        Touch(execution);
                        responseSent = true;
                        continue;
                    }

                    HandleEvent(execution, workflowEvent);

                    if (execution.Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.AwaitingHumanApproval)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
                when (idleCts.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
            {
                // Idle timeout; query run status and continue driving the resumed workflow.
            }

            RunStatus status = await run.GetStatusAsync(timeoutCts.Token).ConfigureAwait(false);

            switch (status)
            {
                case RunStatus.Ended:
                    MarkCompleted(execution);
                    return;

                case RunStatus.PendingRequests:
                    if (execution.Status == WorkflowStatus.AwaitingHumanApproval)
                    {
                        return;
                    }

                    break;

                case RunStatus.Running:
                case RunStatus.Idle:
                    if (!responseSent)
                    {
                        continue;
                    }

                    break;

                default:
                    return;
            }
        }
    }

    private void HandleEvent(WorkflowExecution execution, WorkflowEvent workflowEvent)
    {
        _logger.LogInformation("Event {Type}", workflowEvent.GetType().Name);

        switch (workflowEvent)
        {
            case AgentResponseUpdateEvent updateEvent:
                TryUpdateAgentOutput(execution, updateEvent.ExecutorId, updateEvent.AsResponse(), isFinal: false);
                break;

            case AgentResponseEvent responseEvent:
                TryUpdateAgentOutput(execution, responseEvent.ExecutorId, responseEvent.Response, isFinal: true);
                break;

            case ExecutorInvokedEvent invokedEvent:
                MarkExecutorStarted(execution, invokedEvent.ExecutorId);
                break;

            case ExecutorCompletedEvent completedEvent:
                MarkExecutorCompleted(execution, completedEvent.ExecutorId);
                TryUpdateAgentOutput(execution, completedEvent.ExecutorId, completedEvent.Data, isFinal: true);
                break;

            case ExecutorFailedEvent failedEvent:
                string message = failedEvent.Data?.Message
                    ?? $"Executor '{failedEvent.ExecutorId}' failed.";
                MarkFailed(execution, message);
                break;

            case WorkflowOutputEvent:
                MarkCompleted(execution);
                break;

            case RequestInfoEvent requestInfoEvent
                when requestInfoEvent.Request.TryGetDataAs(out HumanApprovalPrompt? _):
                execution.Status = WorkflowStatus.AwaitingHumanApproval;
                execution.CurrentAgent = null;
                execution.PendingApprovalRequest = requestInfoEvent.Request;
                Touch(execution);
                break;

            case SuperStepCompletedEvent superStepCompletedEvent
                when superStepCompletedEvent.CompletionInfo?.Checkpoint is not null:
                execution.PendingCheckpoint = superStepCompletedEvent.CompletionInfo.Checkpoint;
                Touch(execution);
                break;
        }
    }

    private void MarkCompleted(WorkflowExecution execution)
    {
        execution.Status = WorkflowStatus.Completed;
        execution.CurrentAgent = null;
        execution.PendingApprovalRequest = null;
        execution.PendingCheckpoint = null;
        Touch(execution);
    }

    private void MarkExecutorStarted(WorkflowExecution execution, string executorId)
    {
        string? agentKey = MapExecutorToAgentKey(executorId);
        if (agentKey is null)
        {
            return;
        }

        execution.CurrentAgent = agentKey;

        AgentExecutionState state = GetOrCreateAgentState(execution, agentKey);
        state.Status = WorkflowStatus.Running;

        Touch(execution);
    }

    private void MarkExecutorCompleted(WorkflowExecution execution, string executorId)
    {
        string? agentKey = MapExecutorToAgentKey(executorId);
        if (agentKey is null)
        {
            return;
        }

        AgentExecutionState state = GetOrCreateAgentState(execution, agentKey);
        state.Status = WorkflowStatus.Completed;

        if (string.Equals(execution.CurrentAgent, agentKey, StringComparison.OrdinalIgnoreCase))
        {
            execution.CurrentAgent = null;
        }

        Touch(execution);
    }

    private void TryUpdateAgentOutput(
        WorkflowExecution execution,
        string executorId,
        object? data,
        bool isFinal)
    {
        if (data is null)
        {
            return;
        }

        string? rawOutput = data switch
        {
            AgentResponse response => WorkflowTextExtractor.GetAgentResponseText(response),
            ChatMessage[] messages => WorkflowTextExtractor.FromChatMessages(messages),
            IList<ChatMessage> messages => WorkflowTextExtractor.FromChatMessages(messages),
            IEnumerable<ChatMessage> messages => WorkflowTextExtractor.FromChatMessages(messages),
            ChatMessage message => WorkflowTextExtractor.FromChatMessages([message]),
            string text => text,
            _ => null
        };

        if (rawOutput is null)
        {
            return;
        }

        SaveAgentOutput(execution, executorId, rawOutput, isFinal);
    }

    private void SaveAgentOutput(
        WorkflowExecution execution,
        string executorId,
        string rawOutput,
        bool isFinal)
    {
        string? agentKey = MapExecutorToAgentKey(executorId);
        if (agentKey is null)
        {
            return;
        }

        string normalizedOutput = NormalizeAgentOutput(rawOutput);
        if (string.IsNullOrWhiteSpace(normalizedOutput))
        {
            return;
        }

        AgentExecutionState state = GetOrCreateAgentState(execution, agentKey);
        execution.StreamingBuffers.Remove(agentKey);

        if (!isFinal &&
            execution.AgentOutputs.TryGetValue(agentKey, out string? existingOutput) &&
            normalizedOutput.Length <= existingOutput.Length)
        {
            return;
        }

        execution.AgentOutputs[agentKey] = normalizedOutput;
        state.Output = normalizedOutput;
        Touch(execution);
    }

    private static AgentExecutionState GetOrCreateAgentState(WorkflowExecution execution, string agentKey)
    {
        if (execution.Agents.TryGetValue(agentKey, out AgentExecutionState? state))
        {
            return state;
        }

        state = new AgentExecutionState
        {
            AgentName = agentKey,
            Status = WorkflowStatus.Pending
        };

        execution.Agents[agentKey] = state;
        return state;
    }

    private static string NormalizeAgentOutput(string rawOutput)
    {
        string trimmed = rawOutput.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        const string assistantPrefix = "[assistant]";
        int assistantIndex = trimmed.LastIndexOf(assistantPrefix, StringComparison.OrdinalIgnoreCase);
        if (assistantIndex >= 0)
        {
            trimmed = trimmed[(assistantIndex + assistantPrefix.Length)..].TrimStart();
        }

        return trimmed;
    }

    private static string? MapExecutorToAgentKey(string executorId)
    {
        string id = executorId.Replace("_", "-");

        if (id.Contains("ingestion-translation", StringComparison.OrdinalIgnoreCase))
        {
            return IngestionTranslationKey;
        }

        if (id.Contains("metadata-linking", StringComparison.OrdinalIgnoreCase))
        {
            return MetadataLinkingKey;
        }

        return null;
    }

    private void MarkFailed(WorkflowExecution execution, string reason)
    {
        execution.Status = WorkflowStatus.Failed;
        execution.FailureReason = reason;
        execution.PendingApprovalRequest = null;
        execution.PendingCheckpoint = null;
        Touch(execution);
    }

    private static void Touch(WorkflowExecution execution)
    {
        execution.LastUpdatedUtc = DateTimeOffset.UtcNow;
    }

    private IngestionWorkflowStatusResponse ToResponse(WorkflowExecution execution)
    {
        _store.Save(execution);

        return new IngestionWorkflowStatusResponse
        {
            ExecutionId = execution.ExecutionId,
            SourceId = execution.CorrelationId,
            Status = execution.Status.ToString(),
            AgentOutputs = new IngestionAgentOutputsResponse
            {
                IngestionTranslation = GetOutput(execution, IngestionTranslationKey),
                MetadataLinking = GetOutput(execution, MetadataLinkingKey)
            },
            FailureReason = execution.FailureReason,
            LastUpdatedUtc = execution.LastUpdatedUtc
        };
    }

    private static string? GetOutput(WorkflowExecution execution, string key) =>
        execution.AgentOutputs.TryGetValue(key, out string? value) ? value : null;

    private static async Task SendTurnTokenAsync(StreamingRun run, CancellationToken cancellationToken)
    {
        if (!await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Failed to send TurnToken to the workflow.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
