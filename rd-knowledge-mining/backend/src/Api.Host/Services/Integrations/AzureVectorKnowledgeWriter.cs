using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CohereRndKnowledgeMining.Api.Host.Options;
using Microsoft.Extensions.Options;

namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

public sealed class AzureVectorKnowledgeWriter : IVectorKnowledgeWriter
{
    private readonly SearchClient _searchClient;
    private readonly FoundryEmbeddingClient _embeddingClient;
    private readonly ILogger<AzureVectorKnowledgeWriter> _logger;

    public AzureVectorKnowledgeWriter(
        IOptions<AzureSearchOptions> searchOptions,
        FoundryEmbeddingClient embeddingClient,
        ILogger<AzureVectorKnowledgeWriter> logger)
    {
        AzureSearchOptions options = searchOptions.Value;
        _searchClient = new SearchClient(new Uri(options.Endpoint), options.KnowledgeIndexName, new DefaultAzureCredential());
        _embeddingClient = embeddingClient;
        _logger = logger;
    }

    public async Task WriteAsync(
        string sourceId,
        string executionId,
        string curatedKnowledgeJson,
        CancellationToken cancellationToken)
    {
        MetadataLinkingVectorOutput output = MetadataLinkingVectorOutput.Parse(curatedKnowledgeJson);
        IReadOnlyList<KnowledgeVectorDocument> documents = BuildDocuments(sourceId, executionId, output);

        if (documents.Count == 0)
        {
            _logger.LogInformation(
                "Metadata-linking produced no embeddable chunks for source {SourceId}, execution {ExecutionId}.",
                sourceId,
                executionId);
            return;
        }

        IReadOnlyList<float[]> embeddings = await _embeddingClient
            .EmbedAsync(documents.Select(document => document.ChunkText).ToArray(), cancellationToken)
            .ConfigureAwait(false);

        for (int index = 0; index < documents.Count; index++)
        {
            documents[index].Embedding = embeddings[index];
        }

        IndexDocumentsBatch<KnowledgeVectorDocument> batch = IndexDocumentsBatch.MergeOrUpload(documents);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Indexed {DocumentCount} metadata-linking chunks into Vector DB for source {SourceId}, execution {ExecutionId}.",
            documents.Count,
            sourceId,
            executionId);
    }

    private static IReadOnlyList<KnowledgeVectorDocument> BuildDocuments(
        string sourceId,
        string executionId,
        MetadataLinkingVectorOutput output)
    {
        var documents = new List<KnowledgeVectorDocument>();
        string normalizedSource = SanitizeId(sourceId);
        string normalizedExecution = SanitizeId(executionId);

        documents.Add(new KnowledgeVectorDocument
        {
            Id = $"{normalizedSource}:{normalizedExecution}:summary",
            EntityId = $"RDOC-{normalizedSource}",
            EntityType = "metadata",
            Title = $"Metadata linking summary for {sourceId}",
            PassageId = $"{normalizedSource}:{normalizedExecution}:summary",
            ChunkText = BuildSummaryChunk(output),
            LinkedEntities = output.EntityIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            LineageNarrative = BuildLineageNarrative(output.Links)
        });

        for (int i = 0; i < output.Entities.Count; i++)
        {
            MetadataLinkedEntity entity = output.Entities[i];
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                continue;
            }

            string entityId = ResolveEntityId(output.EntityIds, entity, i, normalizedSource, normalizedExecution);
            IReadOnlyList<string> linkedEntities = output.Links
                .Where(link => ContainsIgnoreCase(link.FromDocument, entity.Name) || ContainsIgnoreCase(link.ToTarget, entity.Name))
                .Select(link => link.ToTarget)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            documents.Add(new KnowledgeVectorDocument
            {
                Id = $"{normalizedSource}:{normalizedExecution}:entity:{i + 1}",
                EntityId = entityId,
                EntityType = NormalizeEntityType(entity.Category),
                Title = entity.Name,
                PassageId = $"{entityId}:{i + 1}",
                ChunkText = BuildEntityChunk(entity, output),
                LinkedEntities = linkedEntities,
                LineageNarrative = BuildLineageNarrative(output.Links.Where(link =>
                    ContainsIgnoreCase(link.FromDocument, entity.Name) ||
                    ContainsIgnoreCase(link.ToTarget, entity.Name)))
            });
        }

        for (int i = 0; i < output.Links.Count; i++)
        {
            MetadataEntityLink link = output.Links[i];
            if (string.IsNullOrWhiteSpace(link.FromDocument) ||
                string.IsNullOrWhiteSpace(link.ToTarget) ||
                string.IsNullOrWhiteSpace(link.Relationship))
            {
                continue;
            }

            documents.Add(new KnowledgeVectorDocument
            {
                Id = $"{normalizedSource}:{normalizedExecution}:link:{i + 1}",
                EntityId = $"LINK-{normalizedSource}-{normalizedExecution}-{i + 1}",
                EntityType = "link",
                Title = $"{link.FromDocument} -> {link.ToTarget}",
                PassageId = $"{normalizedSource}:{normalizedExecution}:link:{i + 1}",
                ChunkText = $"{link.FromDocument} {link.Relationship} {link.ToTarget}.",
                LinkedEntities = [link.FromDocument, link.ToTarget],
                LineageNarrative = $"{link.FromDocument} -> {link.ToTarget} ({link.Relationship})"
            });
        }

        return documents;
    }

    private static string BuildSummaryChunk(MetadataLinkingVectorOutput output) =>
        $"Summary: {output.Summary}{Environment.NewLine}" +
        $"Decision: {output.Decision}{Environment.NewLine}" +
        $"Evidence: {output.Evidence}";

    private static string BuildEntityChunk(MetadataLinkedEntity entity, MetadataLinkingVectorOutput output) =>
        $"Entity: {entity.Name}{Environment.NewLine}" +
        $"Category: {entity.Category}{Environment.NewLine}" +
        $"Version: {entity.Version}{Environment.NewLine}" +
        $"Linking evidence: {output.Evidence}";

    private static string BuildLineageNarrative(IEnumerable<MetadataEntityLink> links)
    {
        string[] linkLines = links
            .Where(link =>
                !string.IsNullOrWhiteSpace(link.FromDocument) &&
                !string.IsNullOrWhiteSpace(link.ToTarget) &&
                !string.IsNullOrWhiteSpace(link.Relationship))
            .Select(link => $"{link.FromDocument} -> {link.ToTarget} ({link.Relationship})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return linkLines.Length == 0
            ? "No lineage relationships were resolved."
            : string.Join("; ", linkLines);
    }

    private static string ResolveEntityId(
        IReadOnlyList<string> outputEntityIds,
        MetadataLinkedEntity entity,
        int index,
        string normalizedSource,
        string normalizedExecution)
    {
        string categoryPrefix = NormalizeCategoryPrefix(entity.Category);

        foreach (string candidate in outputEntityIds)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (candidate.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return $"{categoryPrefix}{normalizedSource}-{normalizedExecution}-{index + 1}";
    }

    private static string NormalizeCategoryPrefix(string category)
    {
        if (ContainsIgnoreCase(category, "trial"))
        {
            return "TRIAL-";
        }

        if (ContainsIgnoreCase(category, "dataset"))
        {
            return "DATASET-";
        }

        if (ContainsIgnoreCase(category, "compound") || ContainsIgnoreCase(category, "drug"))
        {
            return "CMP-";
        }

        if (ContainsIgnoreCase(category, "label"))
        {
            return "LBL-";
        }

        if (ContainsIgnoreCase(category, "regulatory"))
        {
            return "REG-";
        }

        return "RDOC-";
    }

    private static string NormalizeEntityType(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "entity";
        }

        return category.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    private static string SanitizeId(string value)
    {
        string[] fragments = value
            .Split([' ', '/', '\\', ':', '.', ',', ';', '(', ')', '[', ']', '{', '}', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment))
            .ToArray();

        string compact = string.Join("-", fragments);
        return string.IsNullOrWhiteSpace(compact)
            ? "unknown"
            : compact.ToUpperInvariant();
    }

    private static bool ContainsIgnoreCase(string? source, string value) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private sealed class KnowledgeVectorDocument
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("entityId")]
        public required string EntityId { get; init; }

        [JsonPropertyName("entityType")]
        public required string EntityType { get; init; }

        [JsonPropertyName("title")]
        public required string Title { get; init; }

        [JsonPropertyName("passageId")]
        public required string PassageId { get; init; }

        [JsonPropertyName("chunkText")]
        public required string ChunkText { get; init; }

        [JsonPropertyName("lineageNarrative")]
        public required string LineageNarrative { get; init; }

        [JsonPropertyName("linkedEntities")]
        public required IReadOnlyList<string> LinkedEntities { get; init; }

        [JsonPropertyName("embedding")]
        public IReadOnlyList<float> Embedding { get; set; } = [];
    }
}

public sealed class FoundryEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly AzureFoundryModelsOptions _options;
    private readonly DefaultAzureCredential _credential = new();

    public FoundryEmbeddingClient(HttpClient httpClient, IOptions<AzureFoundryModelsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var allEmbeddings = new List<float[]>(texts.Count);
        int batchSize = Math.Max(1, _options.EmbeddingBatchSize);

        foreach (string[] batch in texts.Chunk(batchSize))
        {
            allEmbeddings.AddRange(await EmbedBatchAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        return allEmbeddings;
    }

    private async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> batch,
        CancellationToken cancellationToken)
    {
        string url = BuildEmbeddingsUrl(_options);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        await ApplyAuthorizationAsync(request, cancellationToken).ConfigureAwait(false);
        request.Content = JsonContent.Create(new EmbedRequest
        {
            Model = _options.EmbedModelName,
            Input = batch,
            Dimensions = _options.EmbeddingDimensions
        });

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Foundry embeddings request failed ({(int)response.StatusCode}): {details}");
        }

        EmbedResponse? payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken).ConfigureAwait(false);
        if (payload is null || payload.Data.Count == 0)
        {
            throw new InvalidOperationException("Foundry embeddings response was empty.");
        }

        return payload.Data
            .OrderBy(item => item.Index)
            .Select(item => item.Embedding.ToArray())
            .ToArray();
    }

    private async Task ApplyAuthorizationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            return;
        }

        AccessToken token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://ai.azure.com/.default"]),
            cancellationToken).ConfigureAwait(false);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static string BuildEmbeddingsUrl(AzureFoundryModelsOptions options)
    {
        const string apiVersion = "2024-05-01-preview";
        string endpoint = options.EmbedEndpoint.TrimEnd('/');

        foreach (string suffix in new[] { "/v1/embed", "/v1/embeddings", "/embeddings" })
        {
            if (endpoint.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                endpoint = endpoint[..^suffix.Length].TrimEnd('/');
                break;
            }
        }

        if (!endpoint.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"{endpoint}/openai/deployments/{options.EmbedDeploymentName}";
        }

        return $"{endpoint}/embeddings?api-version={apiVersion}";
    }

    private sealed class EmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required IReadOnlyList<string> Input { get; init; }

        [JsonPropertyName("dimensions")]
        public required int Dimensions { get; init; }
    }

    private sealed class EmbedResponse
    {
        [JsonPropertyName("data")]
        public required IReadOnlyList<EmbedItem> Data { get; init; }
    }

    private sealed class EmbedItem
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("embedding")]
        public required IReadOnlyList<float> Embedding { get; init; }
    }
}
