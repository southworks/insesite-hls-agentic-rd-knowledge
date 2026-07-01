using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// Foundry json_object agents reject input unless the message text contains "json".
    /// </summary>
    private const string JsonObjectInputPrefix =
        "Process the following JSON workflow input and respond with a single JSON object.\n\n";

    /// <summary>Block 1 entry payload: a location pointer for the agent to retrieve raw files via MCP.</summary>
    public static List<ChatMessage> CreateLocationPointerMessage(
        string sourceId,
        string executionId)
    {
        var payload = new
        {
            sourceId,
            executionId
        };

        return [CreateJsonInputMessage(JsonSerializer.Serialize(payload, CompactJsonOptions))];
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

        if (!string.IsNullOrWhiteSpace(previousResult.RawPayloadJson))
        {
            return CreateRichAgentHandoffMessage(correlationId, executionId, previousResult.AgentName, previousResult);
        }

        return CreateJsonMessage(BuildTransitionPayload(correlationId, executionId, previousResult));
    }

    /// <summary>Block 1 handoff: merge workflow context with the full prior-agent JSON payload.</summary>
    public static ChatMessage CreateRichAgentHandoffMessage(
        string correlationId,
        string executionId,
        string priorAgentName,
        AgentStepResult priorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(priorAgentName);
        ArgumentNullException.ThrowIfNull(priorResult);

        if (string.IsNullOrWhiteSpace(priorResult.RawPayloadJson))
        {
            throw new ArgumentException(
                "Rich agent handoff requires RawPayloadJson on the prior step result.",
                nameof(priorResult));
        }

        string json = MergeWorkflowContext(
            correlationId,
            executionId,
            priorAgentName,
            priorResult.RawPayloadJson);

        return CreateJsonInputMessage(json);
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

    private static string MergeWorkflowContext(
        string correlationId,
        string executionId,
        string priorAgentName,
        string rawPayloadJson)
    {
        using JsonDocument document = JsonDocument.Parse(rawPayloadJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Rich agent payload must be a JSON object.");
        }

        JsonObject merged = new()
        {
            ["correlationId"] = correlationId,
            ["sourceId"] = correlationId,
            ["executionId"] = executionId,
            ["priorAgent"] = priorAgentName
        };

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("correlationId")
                || property.NameEquals("sourceId")
                || property.NameEquals("executionId")
                || property.NameEquals("priorAgent"))
            {
                continue;
            }

            merged[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }

        return merged.ToJsonString(CompactJsonOptions);
    }

    private static ChatMessage CreateJsonMessage(object payload)
    {
        string json = JsonSerializer.Serialize(payload, CompactJsonOptions);
        return new ChatMessage(ChatRole.User, json);
    }

    private static ChatMessage CreateJsonInputMessage(string jsonPayload) =>
        new(ChatRole.User, JsonObjectInputPrefix + jsonPayload);
}
