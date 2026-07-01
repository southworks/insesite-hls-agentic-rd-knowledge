namespace CohereRndKnowledgeMining.Api.Host.Options;

public sealed class McpIntegrationOptions
{
    public const string SectionName = "McpIntegration";

    public string KnowledgeSearchEndpoint { get; set; } = string.Empty;
}
