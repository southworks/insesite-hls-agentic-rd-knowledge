using CohereRndKnowledgeMining.Api.Host.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CohereRndKnowledgeMining.Api.Host.Workflow;

public static class IngestionWorkflowConstants
{
    public const string SharedStateScope = "IngestionWorkflowState";

    public const string PendingMessagesKey = "PendingMessages";

    public const string PendingMetadataResultKey = "PendingMetadataResult";

    public const string ApprovalPortId = "IngestionCuratorApproval";

    public const string ReviewerRole = "knowledge-curator";

    public const string IngestionTranslationAgentName = "ingestion-translation-agent";

    public const string MetadataLinkingAgentName = "metadata-linking-agent";
}

/// <summary>
/// Block 1 (Ingestion) workflow: ingestion-translation -> metadata-linking -> Knowledge Curator gate.
/// On approval the curated payload is yielded as the workflow output; the service then writes it to the Vector DB.
/// </summary>
public sealed class IngestionWorkflowFactory
{
    public AgentWorkflow CreateWorkflow(RndKnowledgeAgents agents, string sourceId, string executionId)
    {
        RequestPort approvalPort = RequestPort.Create<HumanApprovalPrompt, HumanApprovalDecision>(
            IngestionWorkflowConstants.ApprovalPortId);

        var agentHostOptions = new AIAgentHostOptions
        {
            EmitAgentResponseEvents = true,
            ForwardIncomingMessages = false
        };

        var ingestionTranslation = agents.IngestionTranslation.BindAsExecutor(agentHostOptions);
        var metadataLinking = agents.MetadataLinking.BindAsExecutor(agentHostOptions);

        FunctionExecutor<IList<ChatMessage>> bridge01 = CreatePayloadBridgeExecutor(
            id: "IngestionBridge01",
            correlationId: sourceId,
            executionId: executionId,
            sourceAgentName: IngestionWorkflowConstants.IngestionTranslationAgentName);
        FunctionExecutor<IList<ChatMessage>> bridge02 = CreatePayloadBridgeExecutor(
            id: "IngestionBridge02",
            correlationId: sourceId,
            executionId: executionId,
            sourceAgentName: IngestionWorkflowConstants.MetadataLinkingAgentName);
        FunctionExecutor<ChatMessage> requestCuratorApproval = CreateCuratorApprovalRequestExecutor(
            id: "IngestionCuratorApprovalRequest",
            correlationId: sourceId,
            executionId: executionId);
        FunctionExecutor<HumanApprovalDecision> applyCuratorDecision = CreateCuratorApprovalDecisionExecutor(
            id: "IngestionCuratorApprovalDecision",
            correlationId: sourceId,
            executionId: executionId);

        return new WorkflowBuilder(ingestionTranslation)
            .AddEdge(ingestionTranslation, bridge01)
            .AddEdge(bridge01, metadataLinking)
            .AddEdge(metadataLinking, bridge02)
            .AddEdge(bridge02, requestCuratorApproval)
            .AddEdge(requestCuratorApproval, approvalPort)
            .AddEdge(approvalPort, applyCuratorDecision)
            .WithOutputFrom(applyCuratorDecision)
            .WithName($"rd-ingestion-{executionId}")
            .WithDescription("Block 1 ingestion workflow with a Knowledge Curator approval gate before persistence.")
            .Build();
    }

    private static FunctionExecutor<ChatMessage> CreateCuratorApprovalRequestExecutor(
        string id,
        string correlationId,
        string executionId)
    {
        return new FunctionExecutor<ChatMessage>(
            id: id,
            handlerAsync: async (message, context, cancellationToken) =>
            {
                string rawOutput = WorkflowTextExtractor.FromMessage(message);
                AgentStepResult result = ParseBridgeOutput(IngestionWorkflowConstants.MetadataLinkingAgentName, rawOutput);
                ChatMessage payload = WorkflowPayloadBuilder.CreateAgentTransitionMessage(
                    correlationId,
                    executionId,
                    result);

                await context.QueueStateUpdateAsync(
                    IngestionWorkflowConstants.PendingMetadataResultKey,
                    result,
                    scopeName: IngestionWorkflowConstants.SharedStateScope,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                await context.QueueStateUpdateAsync(
                    IngestionWorkflowConstants.PendingMessagesKey,
                    new List<ChatMessage> { payload },
                    scopeName: IngestionWorkflowConstants.SharedStateScope,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var prompt = new HumanApprovalPrompt
                {
                    CorrelationId = correlationId,
                    ExecutionId = executionId,
                    ReviewerRole = IngestionWorkflowConstants.ReviewerRole,
                    Summary = "Ingestion and metadata linking completed. Approve to write the curated knowledge to the Vector DB.",
                    ReviewedOutput = rawOutput
                };

                await context.SendMessageAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(HumanApprovalPrompt)]);
    }

    private static FunctionExecutor<HumanApprovalDecision> CreateCuratorApprovalDecisionExecutor(
        string id,
        string correlationId,
        string executionId)
    {
        return new FunctionExecutor<HumanApprovalDecision>(
            id: id,
            handlerAsync: async (decision, context, cancellationToken) =>
            {
                if (!decision.Approved)
                {
                    throw new InvalidOperationException("Ingestion workflow was denied by the knowledge curator.");
                }

                AgentStepResult? pendingResult = await context
                    .ReadStateAsync<AgentStepResult>(
                        IngestionWorkflowConstants.PendingMetadataResultKey,
                        scopeName: IngestionWorkflowConstants.SharedStateScope,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (pendingResult is null)
                {
                    throw new InvalidOperationException("Pending metadata-linking result was not available when resuming the ingestion workflow.");
                }

                ChatMessage curatedKnowledge = WorkflowPayloadBuilder.CreateAgentTransitionMessage(
                    correlationId,
                    executionId,
                    pendingResult);

                await context.YieldOutputAsync(curatedKnowledge.Text, cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: []);
    }

    private static FunctionExecutor<IList<ChatMessage>> CreatePayloadBridgeExecutor(
        string id,
        string correlationId,
        string executionId,
        string sourceAgentName)
    {
        return new FunctionExecutor<IList<ChatMessage>>(
            id: id,
            handlerAsync: async (messages, context, cancellationToken) =>
            {
                string rawOutput = WorkflowTextExtractor.FromLastAssistantMessage(messages);
                AgentStepResult result = ParseBridgeOutput(sourceAgentName, rawOutput);
                ChatMessage payload = string.Equals(
                    sourceAgentName,
                    IngestionWorkflowConstants.IngestionTranslationAgentName,
                    StringComparison.OrdinalIgnoreCase)
                    ? WorkflowPayloadBuilder.CreateIngestionToLinkingTransitionMessage(
                        correlationId,
                        executionId,
                        result)
                    : WorkflowPayloadBuilder.CreateAgentTransitionMessage(
                        correlationId,
                        executionId,
                        result);

                await context.SendMessageAsync(payload, cancellationToken: cancellationToken).ConfigureAwait(false);

                await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken).ConfigureAwait(false);
            },
            sentMessageTypes: [typeof(ChatMessage), typeof(TurnToken)]);
    }

    private static AgentStepResult ParseBridgeOutput(string sourceAgentName, string rawOutput) =>
        AgentStructuredOutputParser.Parse(sourceAgentName, rawOutput);
}
