namespace CohereRndKnowledgeMining.Api.Host.Options;

public sealed class AzureFoundryModelsOptions
{
    public const string SectionName = "AzureFoundryModels";

    public string EmbedDeploymentName { get; set; } = "cohere-embed-v4";

    public string EmbedModelName { get; set; } = "embed-v-4-0";

    public string EmbedEndpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int EmbeddingDimensions { get; set; } = 1024;

    public int EmbeddingBatchSize { get; set; } = 16;
}
