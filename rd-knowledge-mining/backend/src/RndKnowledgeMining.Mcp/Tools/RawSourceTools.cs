using System.ComponentModel;
using RndKnowledgeMining.Mcp.Adapters;
using RndKnowledgeMining.Mcp.Models;
using ModelContextProtocol.Server;

namespace RndKnowledgeMining.Mcp.Tools;

public sealed class RawSourceTools
{
    private readonly RawSourceService _rawSourceService;

    public RawSourceTools(RawSourceService rawSourceService)
    {
        _rawSourceService = rawSourceService;
    }

    [McpServerTool]
    [Description("Lists raw R&D documents uploaded to Microsoft Fabric for the given sourceId. Returns metadata only (itemId, title, sourceType, sourcePath); use read_raw_document to fetch content.")]
    public Task<ListRawDocumentsResponse> ListRawDocuments(
        [Description("Identifier of the ingestion source/batch (e.g. case-04-demo). Files under Files/raw/{sourceId}/ are returned.")]
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        return _rawSourceService.ListAsync(sourceId, cancellationToken);
    }

    [McpServerTool]
    [Description("Reads a single raw R&D document from Microsoft Fabric by sourceId and itemId. Returns the full document content.")]
    public Task<ReadRawDocumentResponse> ReadRawDocument(
        [Description("Identifier of the ingestion source/batch (e.g. case-04-demo).")]
        string sourceId,
        [Description("itemId returned by list_raw_documents (e.g. case-04-demo-001).")]
        string itemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        return _rawSourceService.ReadAsync(sourceId, itemId, cancellationToken);
    }
}
