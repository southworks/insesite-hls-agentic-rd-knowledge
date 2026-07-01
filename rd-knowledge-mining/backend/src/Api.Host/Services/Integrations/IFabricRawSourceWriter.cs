namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

/// <summary>
/// Uploads raw R&amp;D knowledge items to Microsoft Fabric so that the ingestion-translation
/// agent can discover them through MCP tools instead of receiving inline content.
/// </summary>
public interface IFabricRawSourceWriter
{
    Task WriteAsync(string sourceId, IReadOnlyList<RawKnowledgeItem> items, CancellationToken cancellationToken);
}
