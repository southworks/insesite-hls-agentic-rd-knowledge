namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

/// <summary>
/// Retrieves grounding passages from the Vector DB for Block 2 Search &amp; Chat
/// (Cohere Embed query -> Vector DB -> Cohere Rerank -> Top-N).
/// </summary>
public interface IVectorKnowledgeRetriever
{
    Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(string query, int topN, CancellationToken cancellationToken);
}

/// <summary>
/// Placeholder implementation that returns no passages.
/// TODO: replace with a real Cohere Embed + Azure AI Search + Cohere Rerank retriever.
/// </summary>
public sealed class StubVectorKnowledgeRetriever : IVectorKnowledgeRetriever
{
    private readonly ILogger<StubVectorKnowledgeRetriever> _logger;

    public StubVectorKnowledgeRetriever(ILogger<StubVectorKnowledgeRetriever> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(string query, int topN, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[stub] Would retrieve top {TopN} passages from the Vector DB for query: {Query}", topN, query);

        IReadOnlyList<RetrievedPassage> passages = [];
        return Task.FromResult(passages);
    }
}
