namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

/// <summary>
/// Persists curated, linked knowledge into the Vector DB at the end of Block 1 (after Curator approval).
/// </summary>
public interface IVectorKnowledgeWriter
{
    Task WriteAsync(string sourceId, string executionId, string curatedKnowledgeJson, CancellationToken cancellationToken);
}

/// <summary>
/// Placeholder implementation that logs the write instead of embedding + uploading to a vector index.
/// TODO: replace with a real Cohere Embed + Azure AI Search writer.
/// </summary>
public sealed class StubVectorKnowledgeWriter : IVectorKnowledgeWriter
{
    private readonly ILogger<StubVectorKnowledgeWriter> _logger;

    public StubVectorKnowledgeWriter(ILogger<StubVectorKnowledgeWriter> logger)
    {
        _logger = logger;
    }

    public Task WriteAsync(string sourceId, string executionId, string curatedKnowledgeJson, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[stub] Would embed and write curated knowledge to the Vector DB for source {SourceId}, execution {ExecutionId} ({Length} chars).",
            sourceId,
            executionId,
            curatedKnowledgeJson?.Length ?? 0);

        return Task.CompletedTask;
    }
}
