using CohereRndKnowledgeMining.Api.Host.Services;

namespace CohereRndKnowledgeMining.Api.Host.Workflow;

internal static class IngestionHandoffResolver
{
    private static readonly TimeSpan HandoffTimeout = TimeSpan.FromMinutes(20);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static async Task<AgentStepResult> WaitForFinalAgentStepResultAsync(
        InMemoryIngestionWorkflowStore store,
        string executionId,
        string agentOutputKey,
        string agentName,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(HandoffTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WorkflowExecution execution = store.GetRequired(executionId);
            if (execution.FinalAgentStepResults.TryGetValue(agentOutputKey, out AgentStepResult? result))
            {
                return result;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Timed out waiting for the final text response from agent '{agentName}'. " +
            "The agent must finish all MCP tool calls and emit the completed JSON object before the workflow can continue.");
    }

    public static string MapAgentNameToOutputKey(string agentName) =>
        string.Equals(agentName, IngestionWorkflowConstants.MetadataLinkingAgentName, StringComparison.OrdinalIgnoreCase)
            ? IngestionWorkflowConstants.MetadataLinkingOutputKey
            : IngestionWorkflowConstants.IngestionTranslationOutputKey;
}
