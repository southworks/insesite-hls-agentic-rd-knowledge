using System.Text.Json;
using Microsoft.Extensions.Logging;
using RndKnowledgeMining.Mcp.Models;

namespace RndKnowledgeMining.Mcp.Adapters;

public sealed class LocalNormalizedDocumentStore : INormalizedDocumentStore
{
    private readonly string _datasetRoot;
    private readonly ILogger<LocalNormalizedDocumentStore> _logger;

    public LocalNormalizedDocumentStore(string datasetRoot, ILogger<LocalNormalizedDocumentStore> logger)
    {
        _datasetRoot = datasetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

        string batchDirectory = NormalizedDocumentPathBuilder.BuildLocalBatchDirectory(_datasetRoot, sourceId, executionId);
        string documentsDirectory = Path.Combine(batchDirectory, "documents");
        Directory.CreateDirectory(documentsDirectory);

        foreach (NormalizedDocumentHandoffSplitter.NormalizedDocumentArtifact document in split.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string documentPath = NormalizedDocumentPathBuilder.BuildLocalDocumentPath(
                _datasetRoot,
                sourceId,
                executionId,
                document.DocumentId);
            await File.WriteAllTextAsync(documentPath, document.DocumentJson, cancellationToken).ConfigureAwait(false);
        }

        string manifestPath = NormalizedDocumentPathBuilder.BuildLocalManifestPath(_datasetRoot, sourceId, executionId);
        await File.WriteAllTextAsync(manifestPath, split.ManifestJson, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Persisted {DocumentCount} normalized documents for source {SourceId}, execution {ExecutionId} under {BatchDirectory}.",
            split.Documents.Count,
            sourceId,
            executionId,
            batchDirectory);

        return split.ManifestJson;
    }

    public Task<ListNormalizedDocumentsResponse> ListAsync(
        string sourceId,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        string batchDirectory = NormalizedDocumentPathBuilder.BuildLocalBatchDirectory(_datasetRoot, sourceId, executionId);
        string normalizedRoot = Path.Combine(_datasetRoot, "cases", sourceId, "normalized", executionId)
            .Replace('\\', '/');

        if (!Directory.Exists(batchDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Normalized batch directory not found: {batchDirectory}");
        }

        string manifestPath = NormalizedDocumentPathBuilder.BuildLocalManifestPath(_datasetRoot, sourceId, executionId);
        string manifestJson = File.ReadAllText(manifestPath);
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
                    StoragePath = NormalizedDocumentPathBuilder.BuildLocalDocumentPath(
                        _datasetRoot,
                        sourceId,
                        executionId,
                        documentId).Replace('\\', '/')
                });
            }
        }

        int documentsReceived = manifest.RootElement.TryGetProperty("documentsReceived", out JsonElement countElement)
            && countElement.TryGetInt32(out int count)
            ? count
            : summaries.Count;

        return Task.FromResult(new ListNormalizedDocumentsResponse
        {
            SourceId = sourceId,
            ExecutionId = executionId,
            NormalizedRoot = normalizedRoot,
            DocumentsReceived = documentsReceived,
            Documents = summaries
        });
    }

    public Task<ReadNormalizedDocumentResponse> ReadAsync(
        string sourceId,
        string executionId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        string documentPath = NormalizedDocumentPathBuilder.BuildLocalDocumentPath(
            _datasetRoot,
            sourceId,
            executionId,
            documentId);

        if (!File.Exists(documentPath))
        {
            throw new FileNotFoundException(
                $"Normalized document '{documentId}' was not found for source '{sourceId}', execution '{executionId}'.");
        }

        string content = File.ReadAllText(documentPath);
        return Task.FromResult(new ReadNormalizedDocumentResponse
        {
            SourceId = sourceId,
            ExecutionId = executionId,
            DocumentId = documentId,
            StoragePath = documentPath.Replace('\\', '/'),
            Content = content
        });
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
