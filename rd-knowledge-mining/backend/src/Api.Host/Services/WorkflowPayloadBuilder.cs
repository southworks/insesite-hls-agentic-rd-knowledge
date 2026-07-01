using System.Text.Json;
using CohereRndKnowledgeMining.Api.Host.Workflow;
using Microsoft.Extensions.AI;

namespace CohereRndKnowledgeMining.Api.Host.Services;

/// <summary>
/// Builds the JSON <see cref="ChatMessage"/> payloads exchanged between executors in both block workflows.
/// </summary>
public static class WorkflowPayloadBuilder
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>Block 1 entry payload: the raw R&D knowledge items read from Fabric.</summary>
    public static List<ChatMessage> CreateInitialMessages(
        string sourceId,
        string executionId,
        IReadOnlyList<RawKnowledgeItem> items)
    {
        var payload = new
        {
            sourceId,
            executionId,
            items = items.Select(item => new
            {
                itemId = item.ItemId,
                title = item.Title,
                sourceType = item.SourceType,
                sourcePath = item.SourcePath,
                content = item.Content
            })
        };

        return [CreateJsonMessage(payload)];
    }

    /// <summary>
    /// Block 1 entry payload when data has been uploaded to Fabric. The agent receives
    /// only a reference pointer and must use MCP tools to retrieve the raw documents.
    /// </summary>
    public static List<ChatMessage> CreateFabricParamsMessage(
        string sourceId,
        string executionId)
    {
        var payload = new
        {
            sourceId,
            executionId,
            dataSource = "fabric"
        };

        return [CreateJsonMessage(payload)];
    }

    /// <summary>Block 2 Curate entry payload: all accumulated Search &amp; Chat responses.</summary>
    public static List<ChatMessage> CreateCurationInputMessages(
        string sessionId,
        string executionId,
        IReadOnlyList<string> chatResponses)
    {
        var payload = new
        {
            sessionId,
            executionId,
            chatResponses
        };

        return [CreateJsonMessage(payload)];
    }

    /// <summary>Transition payload sent between agents (and into the approval request).</summary>
    public static ChatMessage CreateAgentTransitionMessage(
        string correlationId,
        string executionId,
        AgentStepResult previousResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(previousResult);

        return CreateJsonMessage(BuildTransitionPayload(correlationId, executionId, previousResult));
    }

    private static object BuildTransitionPayload(
        string correlationId,
        string executionId,
        AgentStepResult previousResult) =>
        new
        {
            correlationId,
            executionId,
            summary = previousResult.Summary,
            decision = previousResult.Decision,
            evidence = previousResult.Evidence,
            riskLevel = previousResult.RiskLevel,
            policyRefs = previousResult.PolicyRefs,
            anomalies = previousResult.Anomalies,
            keyFacts = previousResult.KeyFacts,
            flags = previousResult.Flags,
            citations = previousResult.Citations,
            capturedDecisions = previousResult.CapturedDecisions
        };

    private static ChatMessage CreateJsonMessage(object payload)
    {
        string json = JsonSerializer.Serialize(payload, CompactJsonOptions);
        return new ChatMessage(ChatRole.User, json);
    }
}
