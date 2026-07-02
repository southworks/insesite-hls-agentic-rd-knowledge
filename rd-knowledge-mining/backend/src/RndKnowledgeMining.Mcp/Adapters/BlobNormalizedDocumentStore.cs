using System.Text;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RndKnowledgeMining.Mcp.Models;
using RndKnowledgeMining.Mcp.Options;

namespace RndKnowledgeMining.Mcp.Adapters;

public sealed class BlobNormalizedDocumentStore : INormalizedDocumentStore
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _normalizedPrefix;
    private readonly ILogger<BlobNormalizedDocumentStore> _logger;

    public BlobNormalizedDocumentStore(
        IOptions<AzureBlobStorageOptions> options,
        ILogger<BlobNormalizedDocumentStore> logger)
    {
        AzureBlobStorageOptions blobOptions = options.Value;
        _normalizedPrefix = string.IsNullOrWhiteSpace(blobOptions.NormalizedPrefix)
            ? "normalized"
            : blobOptions.NormalizedPrefix.Trim('/');
        _logger = logger;

        if (string.IsNullOrWhiteSpace(blobOptions.ContainerName))
        {
            throw new InvalidOperationException("AzureStorage:ContainerName is required when using blob normalized storage.");
        }

        _containerClient = CreateBlobServiceClient(blobOptions)
            .GetBlobContainerClient(blobOptions.ContainerName);
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
            string blobPath = NormalizedDocumentPathBuilder.BuildDocumentRelativePath(
                _normalizedPrefix,
                sourceId,
                executionId,
                document.DocumentId);
            await UploadTextAsync(blobPath, document.DocumentJson, cancellationToken).ConfigureAwait(false);
        }

        string manifestPath = NormalizedDocumentPathBuilder.BuildManifestRelativePath(
            _normalizedPrefix,
            sourceId,
            executionId);
        await UploadTextAsync(manifestPath, split.ManifestJson, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Persisted {DocumentCount} normalized documents for source {SourceId}, execution {ExecutionId} in container {ContainerName} under {NormalizedRoot}.",
            split.Documents.Count,
            sourceId,
            executionId,
            _containerClient.Name,
            NormalizedDocumentPathBuilder.BuildBatchRoot(_normalizedPrefix, sourceId, executionId));

        return split.ManifestJson;
    }

    public async Task<ListNormalizedDocumentsResponse> ListAsync(
        string sourceId,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        string manifestPath = NormalizedDocumentPathBuilder.BuildManifestRelativePath(
            _normalizedPrefix,
            sourceId,
            executionId);
        string manifestJson = await DownloadTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
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
                        _normalizedPrefix,
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
            NormalizedRoot = NormalizedDocumentPathBuilder.BuildBatchRoot(_normalizedPrefix, sourceId, executionId),
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

        string blobPath = NormalizedDocumentPathBuilder.BuildDocumentRelativePath(
            _normalizedPrefix,
            sourceId,
            executionId,
            documentId);
        string content = await DownloadTextAsync(blobPath, cancellationToken).ConfigureAwait(false);

        return new ReadNormalizedDocumentResponse
        {
            SourceId = sourceId,
            ExecutionId = executionId,
            DocumentId = documentId,
            StoragePath = blobPath,
            Content = content
        };
    }

    private async Task UploadTextAsync(string blobPath, string content, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobPath);
        byte[] payload = Encoding.UTF8.GetBytes(content);
        await using var stream = new MemoryStream(payload);
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> DownloadTextAsync(string blobPath, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobPath);
        try
        {
            Response<BlobDownloadResult> download = await blobClient.DownloadContentAsync(cancellationToken)
                .ConfigureAwait(false);
            return download.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"Normalized blob not found: {blobPath}", ex);
        }
    }

    private static BlobServiceClient CreateBlobServiceClient(AzureBlobStorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new BlobServiceClient(options.ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.BlobServiceUri))
        {
            return new BlobServiceClient(new Uri(options.BlobServiceUri), new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Azure Blob Storage is not configured. Set AzureStorage:ConnectionString, AzureStorage:BlobServiceUri, or AZURE_STORAGE_BLOB_SERVICE_URI.");
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
