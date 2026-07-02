using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RndKnowledgeMining.Mcp.Models;
using RndKnowledgeMining.Mcp.Options;

namespace RndKnowledgeMining.Mcp.Adapters;

public sealed class FabricNormalizedDocumentStore : INormalizedDocumentStore
{
    private readonly FabricLakehouseClient _client;
    private readonly string _normalizedRoot;
    private readonly ILogger<FabricNormalizedDocumentStore> _logger;

    public FabricNormalizedDocumentStore(
        FabricLakehouseClient client,
        IOptions<FabricLakehouseOptions> options,
        ILogger<FabricNormalizedDocumentStore> logger)
    {
        _client = client;
        _normalizedRoot = options.Value.NormalizedRoot.Trim('/');
        _logger = logger;
    }

    public async Task<string> PersistIngestionHandoffAsync(
        string sourceId,
        string executionId,
        string ingestionPayloadJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ingestionPayloadJson);

        NormalizedDocumentHandoffSplitter.SplitResult split =
            NormalizedDocumentHandoffSplitter.Split(ingestionPayloadJson);

        foreach (NormalizedDocumentHandoffSplitter.NormalizedDocumentArtifact document in split.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = NormalizedDocumentPathBuilder.BuildDocumentRelativePath(
                _normalizedRoot,
                sourceId,
                executionId,
                document.DocumentId);
            await _client.UploadFileAsync(relativePath, document.DocumentJson, cancellationToken)
                .ConfigureAwait(false);
        }

        string manifestRelativePath = NormalizedDocumentPathBuilder.BuildManifestRelativePath(
            _normalizedRoot,
            sourceId,
            executionId);
        await _client.UploadFileAsync(manifestRelativePath, split.ManifestJson, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Persisted {DocumentCount} normalized documents for source {SourceId}, execution {ExecutionId} under {NormalizedRoot}.",
            split.Documents.Count,
            sourceId,
            executionId,
            NormalizedDocumentPathBuilder.BuildBatchRoot(_normalizedRoot, sourceId, executionId));

        return split.ManifestJson;
    }

    public async Task<ListNormalizedDocumentsResponse> ListAsync(
        string sourceId,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        string manifestRelativePath = NormalizedDocumentPathBuilder.BuildManifestRelativePath(
            _normalizedRoot,
            sourceId,
            executionId);
        string manifestJson = await _client.ReadFileAsync(manifestRelativePath, cancellationToken).ConfigureAwait(false);
        using JsonDocument manifest = JsonDocument.Parse(manifestJson);

        var summaries = new List<NormalizedDocumentSummary>();
        if (manifest.RootElement.TryGetProperty("documentSummaries", out JsonElement summariesElement)
            && summariesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement summaryElement in summariesElement.EnumerateArray())
            {
                string? documentId = summaryElement.TryGetProperty("documentId", out JsonElement idElement)
                    ? idElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(documentId))
                {
                    continue;
                }

                summaries.Add(new NormalizedDocumentSummary
                {
                    DocumentId = documentId,
                    SourceFile = ReadOptionalString(summaryElement, "sourceFile"),
                    CanonicalType = ReadOptionalString(summaryElement, "canonicalType"),
                    StoragePath = NormalizedDocumentPathBuilder.BuildDocumentRelativePath(
                        _normalizedRoot,
                        sourceId,
                        executionId,
                        documentId)
                });
            }
        }

        int documentsReceived = manifest.RootElement.TryGetProperty("documentsReceived", out JsonElement countElement)
            && countElement.TryGetInt32(out int count)
            ? count
            : summaries.Count;

        return new ListNormalizedDocumentsResponse
        {
            SourceId = sourceId,
            ExecutionId = executionId,
            NormalizedRoot = NormalizedDocumentPathBuilder.BuildBatchRoot(_normalizedRoot, sourceId, executionId),
            DocumentsReceived = documentsReceived,
            Documents = summaries
        };
    }

    public async Task<ReadNormalizedDocumentResponse> ReadAsync(
        string sourceId,
        string executionId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        string relativePath = NormalizedDocumentPathBuilder.BuildDocumentRelativePath(
            _normalizedRoot,
            sourceId,
            executionId,
            documentId);

        string content = await _client.ReadFileAsync(relativePath, cancellationToken).ConfigureAwait(false);
        return new ReadNormalizedDocumentResponse
        {
            SourceId = sourceId,
            ExecutionId = executionId,
            DocumentId = documentId,
            StoragePath = relativePath,
            Content = content
        };
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
