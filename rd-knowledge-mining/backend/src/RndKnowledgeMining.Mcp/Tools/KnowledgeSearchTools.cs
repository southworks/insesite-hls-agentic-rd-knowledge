using System.ComponentModel;
using RndKnowledgeMining.Mcp.Adapters;
using RndKnowledgeMining.Mcp.Models;
using ModelContextProtocol.Server;

namespace RndKnowledgeMining.Mcp.Tools;

public sealed class KnowledgeSearchTools
{
    private readonly IKnowledgeSearchService _knowledgeSearchService;

    public KnowledgeSearchTools(IKnowledgeSearchService knowledgeSearchService)
    {
        _knowledgeSearchService = knowledgeSearchService;
    }

    [McpServerTool]
    [Description("Searches curated R&D knowledge in Azure AI Search using Cohere embed + rerank.")]
    public Task<SearchRdKnowledgeResponse> SearchRdKnowledge(
        string sessionId,
        [Description("Required natural-language query describing the R&D topic, study, compound, endpoint, protocol, dataset, or policy area.")]
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return _knowledgeSearchService.SearchAsync(sessionId, query, topK, cancellationToken);
    }

    [McpServerTool]
    [Description("Resolves document-to-dataset-to-study lineage for a retrieved passage identifier.")]
    public Task<KnowledgeLineageResponse> GetKnowledgeLineage(
        string sessionId,
        [Description("Passage identifier returned by search_rd_knowledge, for example RDOC-PMC6889286:0.")]
        string passageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(passageId);
        return _knowledgeSearchService.GetLineageAsync(sessionId, passageId, cancellationToken);
    }

    [McpServerTool]
    [Description("Indexes approved metadata-linking output into Azure AI Search (Vector DB).")]
    public Task<IndexRdKnowledgeResponse> IndexRdKnowledge(
        [Description("Source identifier used by the ingestion workflow.")]
        string sourceId,
        [Description("Workflow execution identifier.")]
        string executionId,
        [Description("Structured metadata-linking JSON output approved by the curator.")]
        string curatedKnowledgeJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(curatedKnowledgeJson);
        return _knowledgeSearchService.IndexKnowledgeAsync(sourceId, executionId, curatedKnowledgeJson, cancellationToken);
    }
}
