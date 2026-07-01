namespace CohereRndKnowledgeMining.Api.Host.Options;

public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    public string Endpoint { get; set; } = string.Empty;

    public string KnowledgeIndexName { get; set; } = "rd-knowledge-evidence";
}
