namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

/// <summary>
/// Reads raw R&D knowledge from Microsoft Fabric (the upstream source for Block 1).
/// </summary>
public interface IFabricRawSourceReader
{
    Task<IReadOnlyList<RawKnowledgeItem>> ReadAsync(string sourceId, CancellationToken cancellationToken);
}

/// <summary>
/// Placeholder implementation that returns a small sample set so the Ingestion workflow can run end-to-end
/// without a real Fabric connection.
/// TODO: replace with a real Microsoft Fabric (Lakehouse) reader.
/// </summary>
public sealed class StubFabricRawSourceReader : IFabricRawSourceReader
{
    public Task<IReadOnlyList<RawKnowledgeItem>> ReadAsync(string sourceId, CancellationToken cancellationToken)
    {
        IReadOnlyList<RawKnowledgeItem> items =
        [
            new RawKnowledgeItem
            {
                ItemId = $"{sourceId}-001",
                Title = "Sample research article",
                SourceType = "article",
                Content = "Stub content for a research article ingested from Fabric.",
                SourcePath = $"fabric://raw/{sourceId}/article-001"
            }
        ];

        return Task.FromResult(items);
    }
}
