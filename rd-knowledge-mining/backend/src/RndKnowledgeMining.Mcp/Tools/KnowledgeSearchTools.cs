using System.ComponentModel;
using RndKnowledgeMining.Mcp.Adapters;
using RndKnowledgeMining.Mcp.Models;
using ModelContextProtocol.Server;

namespace RndKnowledgeMining.Mcp.Tools;

public sealed class KnowledgeSearchTools
{
    private readonly IKnowledgeSearchService _knowledgeSearchService;
    private readonly INormalizedDocumentStore _normalizedDocumentStore;

    public KnowledgeSearchTools(
        IKnowledgeSearchService knowledgeSearchService,
        INormalizedDocumentStore normalizedDocumentStore)
    {
        _knowledgeSearchService = knowledgeSearchService;
        _normalizedDocumentStore = normalizedDocumentStore;
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
    [Description("Lists normalized ingestion documents persisted for a Block 1 batch. Returns documentId metadata only; use read_normalized_document to fetch each document JSON.")]
    public Task<ListNormalizedDocumentsResponse> ListNormalizedDocuments(
        [Description("Ingestion source/batch id (e.g. case-01-human-review).")]
        string sourceId,
        [Description("Workflow execution id for the ingestion run.")]
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        return _normalizedDocumentStore.ListAsync(sourceId, executionId, cancellationToken);
    }

    [McpServerTool]
    [Description("Reads one normalized ingestion document JSON persisted after ingestion-translation completes.")]
    public Task<ReadNormalizedDocumentResponse> ReadNormalizedDocument(
        [Description("Ingestion source/batch id (e.g. case-01-human-review).")]
        string sourceId,
        [Description("Workflow execution id for the ingestion run.")]
        string executionId,
        [Description("Canonical documentId from list_normalized_documents (e.g. doc-pmc5447962).")]
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        return _normalizedDocumentStore.ReadAsync(sourceId, executionId, documentId, cancellationToken);
    }
}
