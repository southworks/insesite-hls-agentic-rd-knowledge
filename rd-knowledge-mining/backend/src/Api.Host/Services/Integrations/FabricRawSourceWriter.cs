using Azure;
using CohereRndKnowledgeMining.Api.Host.Options;
using Microsoft.Extensions.Logging;

namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

public sealed class FabricRawSourceWriter : IFabricRawSourceWriter
{
    private readonly FabricLakehouseClient _client;
    private readonly string _rawRoot;
    private readonly ILogger<FabricRawSourceWriter> _logger;

    public FabricRawSourceWriter(
        FabricLakehouseClient client,
        FabricLakehouseOptions options,
        ILogger<FabricRawSourceWriter> logger)
    {
        _client = client;
        _rawRoot = options.RawRoot.TrimEnd('/');
        _logger = logger;
    }

    public async Task WriteAsync(string sourceId, IReadOnlyList<RawKnowledgeItem> items, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            _logger.LogInformation("No items to upload for source {SourceId}.", sourceId);
            return;
        }

        _logger.LogInformation("Uploading {Count} raw items to Fabric for source {SourceId}.", items.Count, sourceId);

        int uploaded = 0;
        foreach (var item in items)
        {
            var relativePath = $"{_rawRoot}/{sourceId}/{item.Title}";
            try
            {
                await _client.UploadFileAsync(relativePath, item.Content, cancellationToken).ConfigureAwait(false);
                uploaded++;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to upload {Path}: {Error}", relativePath, ex.Message);
                throw;
            }
        }

        _logger.LogInformation(
            "Uploaded {Uploaded}/{Total} items for source {SourceId} to {RawRoot}/{SourceId}.",
            uploaded, items.Count, sourceId, _rawRoot, sourceId);
    }
}
