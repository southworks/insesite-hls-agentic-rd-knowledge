using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CohereRndKnowledgeMining.Api.Host.Services;

public interface IMetadataLinkingPromptAgentService
{
    Task<string> LinkMetadataAsync(string content, CancellationToken cancellationToken);
}

public sealed class MetadataLinkingPromptAgentService : IMetadataLinkingPromptAgentService
{
    private readonly MetadataLinkingPromptAgentProvider _agentProvider;
    private readonly IMcpExternalDataClient _mcpClient;

    public MetadataLinkingPromptAgentService(
        MetadataLinkingPromptAgentProvider agentProvider,
        IMcpExternalDataClient mcpClient)
    {
        _agentProvider = agentProvider;
        _mcpClient = mcpClient;
    }

    public async Task<string> LinkMetadataAsync(string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Content is required.");
        }

        AIAgent agent = await _agentProvider
            .GetMetadataLinkingPromptAgentAsync(cancellationToken)
            .ConfigureAwait(false);

        string instructions = await _agentProvider
            .GetMetadataLinkingInstructionsAsync(cancellationToken)
            .ConfigureAwait(false);

        string outputSchema = await _agentProvider
            .GetOutputSchemaAsync(cancellationToken)
            .ConfigureAwait(false);

        MetadataLinkingPromptAgentProvider.MpcConfiguration mcpConfig = await _agentProvider
            .GetMcpConfigurationAsync(cancellationToken)
            .ConfigureAwait(false);

        string enrichedContent = content;
        if (mcpConfig.Enabled)
        {
            string externalQuery = mcpConfig.QueryTemplate.Replace("{{input}}", content, StringComparison.Ordinal);
            IReadOnlyList<string> externalContext = await _mcpClient
                .QueryAsync(mcpConfig.ServerName, externalQuery, mcpConfig.MaxItems, cancellationToken)
                .ConfigureAwait(false);

            if (externalContext.Count > 0)
            {
                string contextBlock = string.Join(Environment.NewLine, externalContext.Select((item, index) => $"[{index + 1}] {item}"));
                enrichedContent = $"Input:{Environment.NewLine}{content}{Environment.NewLine}{Environment.NewLine}External MCP context:{Environment.NewLine}{contextBlock}";
            }
        }

        string schemaBlock = string.IsNullOrWhiteSpace(outputSchema)
            ? string.Empty
            : $"Output schema (JSON):{Environment.NewLine}{outputSchema}{Environment.NewLine}{Environment.NewLine}";

        string promptBody = $"{schemaBlock}{enrichedContent}";

        string prompt = string.IsNullOrWhiteSpace(instructions)
            ? promptBody
            : $"{instructions}{Environment.NewLine}{Environment.NewLine}{promptBody}";

        AgentResponse response = await agent
            .RunAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Messages?
            .LastOrDefault(message => message.Role == ChatRole.Assistant)?
            .Text?
            .Trim()
            ?? string.Empty;
    }
}
