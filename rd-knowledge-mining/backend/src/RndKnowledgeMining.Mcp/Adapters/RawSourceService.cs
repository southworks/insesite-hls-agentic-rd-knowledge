using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RndKnowledgeMining.Mcp.Models;
using RndKnowledgeMining.Mcp.Options;

namespace RndKnowledgeMining.Mcp.Adapters;

/// <summary>
/// Lists and reads raw R&amp;D documents from Microsoft Fabric OneLake scoped to a sourceId.
/// Documents live under <c>{RawRoot}/{sourceId}/</c> in the configured lakehouse.
/// </summary>
public sealed class FabricRawSourceService : IRawSourceService
{
    private readonly FabricLakehouseClient _client;
    private readonly string _rawRoot;
    private readonly ILogger<FabricRawSourceService> _logger;

    public FabricRawSourceService(
        FabricLakehouseClient client,
        IOptions<FabricLakehouseOptions> options,
        ILogger<FabricRawSourceService> logger)
    {
        _client = client;
        _rawRoot = options.Value.RawRoot.Trim('/');
        _logger = logger;
    }

    public async Task<ListRawDocumentsResponse> ListAsync(string sourceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        var files = await ListSourceFilesAsync(sourceId, cancellationToken).ConfigureAwait(false);

        var documents = files
            .Select(file => BuildSummary(sourceId, file))
            .ToList();

        _logger.LogInformation(
            "Listed {Count} raw documents for source {SourceId}",
            documents.Count,
            sourceId);

        return new ListRawDocumentsResponse
        {
            SourceId = sourceId,
            Documents = documents
        };
    }

    public async Task<ReadRawDocumentResponse> ReadAsync(string sourceId, string fileName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var relativePath = $"{_rawRoot}/{sourceId}/{fileName}";

        string content;
        try
        {
            content = await _client.ReadFileAsync(relativePath, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException(
                $"Fabric file '{relativePath}' was not found for source '{sourceId}'.");
        }

        return new ReadRawDocumentResponse
        {
            Title = fileName,
            SourceType = RawSourceTypeInference.InferSourceType(fileName),
            SourcePath = relativePath,
            Content = RawDocumentContentPreparer.PrepareForAgent(fileName, content.Trim())
        };
    }

    private async Task<IReadOnlyList<string>> ListSourceFilesAsync(string sourceId, CancellationToken cancellationToken)
    {
        var sourcePrefix = $"/{_rawRoot}/{sourceId}/";

        IReadOnlyList<string> allFiles;
        try
        {
            allFiles = await _client.ListFilesAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Filesystem root not found (404) while listing source {SourceId}", sourceId);
            return [];
        }

        var files = allFiles
            .Where(p => p.Contains(sourcePrefix, StringComparison.OrdinalIgnoreCase) && !p.EndsWith('/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            _logger.LogInformation("No files found with prefix {SourcePrefix}", sourcePrefix);
        }

        return files;
    }

    private RawDocumentSummary BuildSummary(string sourceId, string path)
    {
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        return new RawDocumentSummary
        {
            FileName = fileName,
            SourceType = RawSourceTypeInference.InferSourceType(fileName),
            SourcePath = $"{_rawRoot}/{sourceId}/{fileName}"
        };
    }
}
