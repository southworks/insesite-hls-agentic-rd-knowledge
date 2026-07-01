using System.Text.Json;
using System.Text.Json.Serialization;
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

    public async Task<IndexRdKnowledgeResponse> IndexKnowledgeAsync(
        string sourceId,
        string executionId,
        string curatedKnowledgeJson,
        CancellationToken cancellationToken = default)
    {
        MetadataLinkingVectorOutput output = MetadataLinkingVectorOutput.Parse(curatedKnowledgeJson);
        List<KnowledgeSearchDocument> documents = BuildDocuments(sourceId, executionId, output);

        if (documents.Count == 0)
        {
            return new IndexRdKnowledgeResponse
            {
                SourceId = sourceId,
                ExecutionId = executionId,
                IndexedDocuments = 0,
                Status = "No content to index"
            };
        }

        IReadOnlyList<float[]> embeddings = await _embeddingService
            .EmbedAsync(documents.Select(document => document.ChunkText).ToArray(), cancellationToken)
            .ConfigureAwait(false);

        for (int i = 0; i < documents.Count; i++)
        {
            documents[i].Embedding = embeddings[i];
        }

        IndexDocumentsBatch<KnowledgeSearchDocument> batch = IndexDocumentsBatch.MergeOrUpload(documents);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new IndexRdKnowledgeResponse
        {
            SourceId = sourceId,
            ExecutionId = executionId,
            IndexedDocuments = documents.Count,
            Status = "Indexed"
        };
    }

    private static List<KnowledgeSearchDocument> BuildDocuments(
        string sourceId,
        string executionId,
        MetadataLinkingVectorOutput output)
    {
        var documents = new List<KnowledgeSearchDocument>();
        string normalizedSource = SanitizeId(sourceId);
        string normalizedExecution = SanitizeId(executionId);

        documents.Add(new KnowledgeSearchDocument
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

            documents.Add(new KnowledgeSearchDocument
            {
                Id = $"{normalizedSource}:{normalizedExecution}:entity:{i + 1}",
                EntityId = entityId,
                EntityType = NormalizeEntityType(entity.Category),
                Title = entity.Name,
                PassageId = $"{entityId}:{i + 1}",
                ChunkText = BuildEntityChunk(entity, output),
                LinkedEntities = output.Links
                    .Where(link => ContainsIgnoreCase(link.FromDocument, entity.Name) || ContainsIgnoreCase(link.ToTarget, entity.Name))
                    .Select(link => link.ToTarget)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
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

            documents.Add(new KnowledgeSearchDocument
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

    private sealed class MetadataLinkingVectorOutput
    {
        [JsonPropertyName("summary")]
        public string Summary { get; init; } = string.Empty;

        [JsonPropertyName("decision")]
        public string Decision { get; init; } = string.Empty;

        [JsonPropertyName("evidence")]
        public string Evidence { get; init; } = string.Empty;

        [JsonPropertyName("entities")]
        public IReadOnlyList<MetadataLinkedEntity> Entities { get; init; } = [];

        [JsonPropertyName("links")]
        public IReadOnlyList<MetadataEntityLink> Links { get; init; } = [];

        [JsonPropertyName("entityIds")]
        public IReadOnlyList<string> EntityIds { get; init; } = [];

        public static MetadataLinkingVectorOutput Parse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new InvalidOperationException("Metadata-linking output was empty. Expected structured JSON output.");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            MetadataLinkingVectorOutput? parsed = JsonSerializer.Deserialize<MetadataLinkingVectorOutput>(rawJson, options);
            if (parsed is null)
            {
                throw new InvalidOperationException("Metadata-linking output could not be parsed.");
            }

            return parsed;
        }
    }

    private sealed class MetadataLinkedEntity
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;
    }

    private sealed class MetadataEntityLink
    {
        [JsonPropertyName("fromDocument")]
        public string FromDocument { get; init; } = string.Empty;

        [JsonPropertyName("toTarget")]
        public string ToTarget { get; init; } = string.Empty;

        [JsonPropertyName("relationship")]
        public string Relationship { get; init; } = string.Empty;
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
