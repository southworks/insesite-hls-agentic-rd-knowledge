using System.Text.Json;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using RndKnowledgeMining.Mcp.Models;
using RndKnowledgeMining.Mcp.Options;
using Microsoft.Extensions.Options;

namespace RndKnowledgeMining.Mcp.Adapters;

public sealed class KnowledgeIndexAdapter
{
    private readonly SearchClient _searchClient;
    private readonly FoundryEmbeddingService _embeddingService;
    private readonly FoundryRerankService _rerankService;

    public KnowledgeIndexAdapter(
        SearchIndexClient indexClient,
        FoundryEmbeddingService embeddingService,
        FoundryRerankService rerankService,
        IOptions<AzureSearchOptions> options)
    {
        _searchClient = indexClient.GetSearchClient(options.Value.KnowledgeIndexName);
        _embeddingService = embeddingService;
        _rerankService = rerankService;
    }

    public async Task<SearchRdKnowledgeResponse> SearchAsync(
        string sessionId,
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var queryEmbedding = (await _embeddingService.EmbedAsync([query], cancellationToken)).Single();
        var searchOptions = new SearchOptions
        {
            Size = Math.Max(topK * 3, topK),
            Filter = "entityType ne 'metadata'",
            Select = { "entityId", "entityType", "title", "passageId", "chunkText" }
        };

        searchOptions.VectorSearch = new VectorSearchOptions
        {
            Queries =
            {
                new VectorizedQuery(queryEmbedding)
                {
                    KNearestNeighborsCount = searchOptions.Size,
                    Fields = { "embedding" }
                }
            }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(null, searchOptions, cancellationToken);
        var candidates = new List<KnowledgeSearchCandidate>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            if (result.Document is null)
            {
                continue;
            }

            candidates.Add(new KnowledgeSearchCandidate
            {
                EntityId = GetString(result.Document, "entityId"),
                EntityType = GetString(result.Document, "entityType"),
                Title = GetString(result.Document, "title"),
                PassageId = GetString(result.Document, "passageId"),
                ChunkText = GetString(result.Document, "chunkText")
            });
        }

        if (candidates.Count == 0)
        {
            return new SearchRdKnowledgeResponse
            {
                SessionId = sessionId,
                Query = query,
                Matches = []
            };
        }

        IReadOnlyList<KnowledgePassageMatch> matches;
        if (candidates.Count <= topK)
        {
            matches = candidates.Select(candidate => ToMatch(candidate)).ToArray();
        }
        else
        {
            var reranked = await _rerankService.RerankAsync(
                query,
                candidates.Select(candidate => $"{candidate.Title}\n{candidate.ChunkText}").ToArray(),
                topK,
                cancellationToken);

            matches = reranked
                .Select(result => ToMatch(candidates[result.Index], result.Score))
                .ToArray();
        }

        return new SearchRdKnowledgeResponse
        {
            SessionId = sessionId,
            Query = query,
            Matches = matches
        };
    }

    public async Task<IndexRdKnowledgeResponse> IndexAsync(
        string sessionId,
        string entityId,
        string entityType,
        string title,
        string chunkText,
        IReadOnlyList<string>? linkedEntities = null,
        string? lineageNarrative = null,
        string? passageId = null,
        CancellationToken cancellationToken = default)
    {
        var batchResponse = await IndexBatchAsync(
            sessionId,
            [
                new IndexRdKnowledgeBatchItem
                {
                    EntityId = entityId,
                    EntityType = entityType,
                    Title = title,
                    ChunkText = chunkText,
                    PassageId = passageId,
                    LinkedEntities = linkedEntities,
                    LineageNarrative = lineageNarrative
                }
            ],
            cancellationToken);

        return batchResponse.Results[0];
    }

    public async Task<IndexRdKnowledgeBatchResponse> IndexBatchAsync(
        string sessionId,
        IReadOnlyList<IndexRdKnowledgeBatchItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one indexing item is required.", nameof(items));
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(item.EntityId);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.EntityType);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.Title);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.ChunkText);
        }

        var chunkTexts = items
            .Select(item => item.ChunkText.Trim())
            .ToArray();

        var embeddings = await _embeddingService.EmbedAsync(chunkTexts, cancellationToken);
        var documents = new List<KnowledgeSearchDocument>(items.Count);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var normalizedPassageId = NormalizePassageId(item.EntityId, item.ChunkText, item.PassageId);
            var normalizedLinkedEntities = (item.LinkedEntities ?? [])
                .Where(linkedEntity => !string.IsNullOrWhiteSpace(linkedEntity))
                .Select(linkedEntity => linkedEntity.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            documents.Add(new KnowledgeSearchDocument
            {
                Id = normalizedPassageId,
                EntityId = item.EntityId.Trim(),
                EntityType = item.EntityType.Trim(),
                Title = item.Title.Trim(),
                PassageId = normalizedPassageId,
                ChunkText = item.ChunkText.Trim(),
                LinkedEntities = normalizedLinkedEntities,
                LineageNarrative = string.IsNullOrWhiteSpace(item.LineageNarrative)
                    ? string.Empty
                    : item.LineageNarrative.Trim(),
                Embedding = embeddings[index]
            });
        }

        var batch = IndexDocumentsBatch.MergeOrUpload(documents);
        var response = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
        var resultsByKey = response.Value.Results
            .ToDictionary(result => result.Key, StringComparer.OrdinalIgnoreCase);

        var results = new List<IndexRdKnowledgeResponse>(documents.Count);
        var indexedCount = 0;

        foreach (var document in documents)
        {
            var indexed = resultsByKey.TryGetValue(document.Id, out IndexingResult? indexingResult)
                && indexingResult.Succeeded;

            if (indexed)
            {
                indexedCount++;
            }

            results.Add(new IndexRdKnowledgeResponse
            {
                SessionId = sessionId.Trim(),
                EntityId = document.EntityId,
                PassageId = document.PassageId,
                Indexed = indexed
            });
        }

        if (indexedCount < documents.Count)
        {
            throw new InvalidOperationException(
                $"Failed to index all passages for session '{sessionId}'. Indexed {indexedCount} of {documents.Count}.");
        }

        return new IndexRdKnowledgeBatchResponse
        {
            SessionId = sessionId.Trim(),
            Requested = documents.Count,
            Indexed = indexedCount,
            Results = results
        };
    }

    public async Task<KnowledgeLineageResponse> GetLineageAsync(
        string sessionId,
        string passageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(passageId);

        try
        {
            var response = await _searchClient.GetDocumentAsync<KnowledgeSearchDocument>(
                passageId.Trim(),
                new GetDocumentOptions
                {
                    SelectedFields = { "entityId", "passageId", "linkedEntities", "lineageNarrative" }
                },
                cancellationToken: cancellationToken);

            if (string.Equals(response.Value.EntityType, "metadata", StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException($"No lineage record was found for passage '{passageId}'.");
            }

            return new KnowledgeLineageResponse
            {
                SessionId = sessionId.Trim(),
                PassageId = response.Value.PassageId,
                EntityId = response.Value.EntityId,
                LinkedEntities = response.Value.LinkedEntities,
                Lineage = response.Value.LineageNarrative
            };
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            throw new KeyNotFoundException($"No lineage record was found for passage '{passageId}'.", exception);
        }
    }

    private static KnowledgePassageMatch ToMatch(KnowledgeSearchCandidate candidate, double score = 1) =>
        new()
        {
            PassageId = candidate.PassageId ?? string.Empty,
            EntityId = candidate.EntityId ?? string.Empty,
            EntityType = candidate.EntityType ?? string.Empty,
            Title = candidate.Title ?? string.Empty,
            Snippet = Truncate(candidate.ChunkText ?? string.Empty),
            Score = score
        };

    private static string Truncate(string text) =>
        text.Length <= ToolResponseLimits.MaxEvidenceSnippetLength
            ? text
            : text[..ToolResponseLimits.MaxEvidenceSnippetLength] + "...";

    private static string? GetString(SearchDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out object? value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            _ => value.ToString()
        };
    }

    private static string NormalizePassageId(string entityId, string chunkText, string? passageId)
    {
        if (!string.IsNullOrWhiteSpace(passageId))
        {
            return passageId.Trim();
        }

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(chunkText.Trim())));

        return $"{entityId.Trim()}:{hash[..12].ToLowerInvariant()}";
    }

    private sealed class KnowledgeSearchCandidate
    {
        public string? EntityId { get; init; }

        public string? EntityType { get; init; }

        public string? Title { get; init; }

        public string? PassageId { get; init; }

        public string? ChunkText { get; init; }
    }

    public sealed class KnowledgeSearchDocument
    {
        public required string Id { get; set; }

        public required string EntityId { get; set; }

        public required string EntityType { get; set; }

        public required string Title { get; set; }

        public required string PassageId { get; set; }

        public required string ChunkText { get; set; }

        public IReadOnlyList<string> LinkedEntities { get; set; } = [];

        public string LineageNarrative { get; set; } = string.Empty;

        public IReadOnlyList<float> Embedding { get; set; } = [];
    }
}
