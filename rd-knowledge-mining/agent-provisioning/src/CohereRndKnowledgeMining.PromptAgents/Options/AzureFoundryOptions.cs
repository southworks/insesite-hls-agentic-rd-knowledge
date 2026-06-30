namespace CohereRndKnowledgeMining.Api.Host.Options;

public sealed class AzureFoundryOptions
{
    public const string SectionName = "AzureFoundry";

    public string ProjectEndpoint { get; set; } = string.Empty;

    public string MetadataLinkingPromptAgentName { get; set; } = "metadata-linking-agent";

    public string MetadataLinkingPromptAgentSpecPath { get; set; } =
        "agents/metadata-linking/agent.json";
}
