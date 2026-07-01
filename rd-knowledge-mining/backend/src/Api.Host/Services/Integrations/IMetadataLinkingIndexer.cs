namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

public interface IMetadataLinkingIndexer
{
    Task IndexApprovedMetadataAsync(
        string sourceId,
        string executionId,
        string curatedKnowledgeJson,
        CancellationToken cancellationToken);
}

public sealed class FallbackMetadataLinkingIndexer : IMetadataLinkingIndexer
{
    private readonly IVectorKnowledgeWriter _vectorKnowledgeWriter;

    public FallbackMetadataLinkingIndexer(IVectorKnowledgeWriter vectorKnowledgeWriter)
    {
        _vectorKnowledgeWriter = vectorKnowledgeWriter;
    }

    public Task IndexApprovedMetadataAsync(
        string sourceId,
        string executionId,
        string curatedKnowledgeJson,
        CancellationToken cancellationToken) =>
        _vectorKnowledgeWriter.WriteAsync(sourceId, executionId, curatedKnowledgeJson, cancellationToken);
}
