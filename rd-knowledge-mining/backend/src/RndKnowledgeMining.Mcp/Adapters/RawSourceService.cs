using System.Text.RegularExpressions;
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
public sealed class RawSourceService
{
    private static readonly Regex ItemIdPattern = new(
        @"^(?<sourceId>.+)-(?<index>\d{3,})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly FabricLakehouseClient _client;
    private readonly string _rawRoot;
    private readonly ILogger<RawSourceService> _logger;

    public RawSourceService(
        FabricLakehouseClient client,
        IOptions<FabricLakehouseOptions> options,
        ILogger<RawSourceService> logger)
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
            .Select((file, index) => BuildSummary(sourceId, file, index))
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

    public async Task<ReadRawDocumentResponse> ReadAsync(string sourceId, string itemId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var files = await ListSourceFilesAsync(sourceId, cancellationToken).ConfigureAwait(false);
        var match = ItemIdPattern.Match(itemId);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"itemId '{itemId}' is not in the expected format '{{sourceId}}-{{index}}' (e.g. '{sourceId}-001').",
                nameof(itemId));
        }

        if (!match.Groups["sourceId"].Value.Equals(sourceId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"itemId '{itemId}' does not belong to sourceId '{sourceId}'.",
                nameof(itemId));
        }

        if (!int.TryParse(match.Groups["index"].Value, out var index) || index < 1 || index > files.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(itemId),
                $"itemId '{itemId}' references index {index} but only {files.Count} document(s) are available for source '{sourceId}'.");
        }

        var file = files[index - 1];
        var fileName = file[(file.LastIndexOf('/') + 1)..];
        var relativePath = $"{_rawRoot}/{sourceId}/{fileName}";

        string content;
        try
        {
            content = await _client.ReadFileAsync(relativePath, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException(
                $"Fabric file '{relativePath}' was not found for itemId '{itemId}'.");
        }

        return new ReadRawDocumentResponse
        {
            ItemId = itemId,
            Title = fileName,
            SourceType = RawSourceTypeInference.InferSourceType(fileName),
            SourcePath = relativePath,
            Content = content.Trim()
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

    private RawDocumentSummary BuildSummary(string sourceId, string path, int index)
    {
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        return new RawDocumentSummary
        {
            ItemId = $"{sourceId}-{index + 1:D3}",
            Title = fileName,
            SourceType = RawSourceTypeInference.InferSourceType(fileName),
            SourcePath = $"{_rawRoot}/{sourceId}/{fileName}"
        };
    }
}
