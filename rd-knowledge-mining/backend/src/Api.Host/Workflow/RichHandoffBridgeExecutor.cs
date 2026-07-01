using CohereRndKnowledgeMining.Api.Host.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using RndKnowledgeMining.Mcp.Adapters;

namespace CohereRndKnowledgeMining.Api.Host.Workflow;

/// <summary>
/// Chat-protocol bridge that waits for upstream agent turn completion before parsing handoff JSON.
/// Buffers intermediate MCP tool-call forwards across turns and only forwards downstream when a
/// recognized rich payload is available.
/// </summary>
internal sealed class RichHandoffBridgeExecutor : ChatProtocolExecutor
{
    private readonly string _correlationId;
    private readonly string _executionId;
    private readonly string _sourceAgentName;
    private readonly string _accumulatedMessagesKey;
    private readonly INormalizedDocumentStore? _normalizedDocumentStore;
    private readonly IngestionSourceDocumentCache? _sourceDocumentCache;
    private readonly bool _persistNormalizedDocuments;

    public RichHandoffBridgeExecutor(
        string id,
        string correlationId,
        string executionId,
        string sourceAgentName,
        INormalizedDocumentStore? normalizedDocumentStore = null,
        IngestionSourceDocumentCache? sourceDocumentCache = null,
        bool persistNormalizedDocuments = false)
        : base(
            id,
            new ChatProtocolExecutorOptions { AutoSendTurnToken = false },
            declareCrossRunShareable: false)
    {
        _correlationId = correlationId;
        _executionId = executionId;
        _sourceAgentName = sourceAgentName;
        _accumulatedMessagesKey = $"{id}.AccumulatedMessages";
        _normalizedDocumentStore = normalizedDocumentStore;
        _sourceDocumentCache = sourceDocumentCache;
        _persistNormalizedDocuments = persistNormalizedDocuments;
    }

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> accumulated = await ReadAccumulatedMessagesAsync(context, cancellationToken)
            .ConfigureAwait(false);
        accumulated.AddRange(messages);

        await context.QueueStateUpdateAsync(
            _accumulatedMessagesKey,
            accumulated,
            scopeName: IngestionWorkflowConstants.SharedStateScope,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        string rawOutput = ExtractBridgeOutput(_sourceAgentName, accumulated);
        if (!AgentStructuredOutputParser.TryParseRichPayload(_sourceAgentName, rawOutput, out AgentStepResult? parsedResult)
            || parsedResult is null)
        {
            return;
        }

        AgentStepResult result = AgentStructuredOutputParser.Parse(_sourceAgentName, rawOutput);
        ChatMessage payload;

        if (_persistNormalizedDocuments
            && _normalizedDocumentStore is not null
            && string.Equals(
                _sourceAgentName,
                IngestionWorkflowConstants.IngestionTranslationAgentName,
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(result.RawPayloadJson))
        {
            string payloadJson = result.RawPayloadJson;
            if (_sourceDocumentCache?.TryGet(_executionId, out IReadOnlyList<RawKnowledgeItem>? sourceItems) == true
                && sourceItems is not null)
            {
                payloadJson = IngestionHandoffEnricher.Enrich(payloadJson, sourceItems);
            }

            string manifestJson = await _normalizedDocumentStore
                .PersistIngestionHandoffAsync(
                    _correlationId,
                    _executionId,
                    payloadJson,
                    cancellationToken)
                .ConfigureAwait(false);

            payload = WorkflowPayloadBuilder.CreateMetadataLinkingHandoffMessage(
                _correlationId,
                _executionId,
                manifestJson);
        }
        else
        {
            payload = WorkflowPayloadBuilder.CreateRichAgentHandoffMessage(
                _correlationId,
                _executionId,
                _sourceAgentName,
                result);
        }

        await context.SendMessageAsync(payload, cancellationToken: cancellationToken).ConfigureAwait(false);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await context.QueueStateUpdateAsync(
            _accumulatedMessagesKey,
            new List<ChatMessage>(),
            scopeName: IngestionWorkflowConstants.SharedStateScope,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<ChatMessage>> ReadAccumulatedMessagesAsync(
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        List<ChatMessage>? accumulated = await context
            .ReadStateAsync<List<ChatMessage>>(
                _accumulatedMessagesKey,
                scopeName: IngestionWorkflowConstants.SharedStateScope,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return accumulated ?? [];
    }

    private static string ExtractBridgeOutput(string sourceAgentName, IList<ChatMessage> messages)
    {
        string fromLast = WorkflowTextExtractor.FromLastAssistantMessage(messages);
        if (AgentStructuredOutputParser.TryParseRichPayload(sourceAgentName, fromLast, out _))
        {
            return fromLast;
        }

        string aggregated = WorkflowTextExtractor.CollectHandoffSourceText(messages);
        if (AgentStructuredOutputParser.TryParseRichPayload(sourceAgentName, aggregated, out _))
        {
            return aggregated;
        }

        return !string.IsNullOrWhiteSpace(fromLast) ? fromLast : aggregated;
    }
}
