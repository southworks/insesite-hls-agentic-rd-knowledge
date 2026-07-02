using System.Text.Json;
using CohereRndKnowledgeMining.Api.Host.Contracts;
using CohereRndKnowledgeMining.Api.Host.Services;
using CohereRndKnowledgeMining.Api.Host.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace CohereRndKnowledgeMining.Api.Host.Controllers;

/// <summary>
/// Vector DB summary endpoint. Aggregates counts from completed ingestion executions.
/// </summary>
[ApiController]
[Route("api/rd-knowledge/vector-db")]
public sealed class VectorDbController : ControllerBase
{
    private readonly InMemoryIngestionWorkflowStore _store;
    private readonly ILogger<VectorDbController> _logger;

    public VectorDbController(
        InMemoryIngestionWorkflowStore store,
        ILogger<VectorDbController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpGet("summary")]
    public ActionResult<VectorDbStoreSummaryResponse> GetSummary()
    {
        try
        {
            IReadOnlyList<WorkflowExecution> executions = _store.GetAll();

            var completedIngestions = executions
                .Where(e => e.Status == WorkflowStatus.Completed)
                .ToList();

            int totalStudies = completedIngestions
                .Select(e => e.CorrelationId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            int totalDocuments = 0;
            int totalEntities = 0;
            int totalLinks = 0;

            foreach (WorkflowExecution execution in completedIngestions)
            {
                totalDocuments += CountDocuments(execution);
                totalEntities += CountEntities(execution);
                totalLinks += CountLinks(execution);
            }

            WorkflowExecution? lastIngestion = completedIngestions
                .OrderByDescending(e => e.LastUpdatedUtc)
                .FirstOrDefault();

            return Ok(new VectorDbStoreSummaryResponse
            {
                TotalStudies = totalStudies,
                TotalDocuments = totalDocuments,
                TotalEntities = totalEntities,
                TotalLinks = totalLinks,
                LastIngestionAt = lastIngestion?.LastUpdatedUtc,
                LastIngestedStudyId = lastIngestion?.CorrelationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute Vector DB summary.");
            return Problem(
                detail: ex.Message,
                title: "Failed to compute Vector DB summary.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static int CountDocuments(WorkflowExecution execution)
    {
        if (!execution.AgentOutputs.TryGetValue(
                IngestionWorkflowConstants.IngestionTranslationOutputKey, out string? raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("summary", out var summary) &&
                summary.ValueKind == JsonValueKind.Object &&
                summary.TryGetProperty("documentsReceived", out var count))
            {
                return count.GetInt32();
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return 0;
    }

    private static int CountEntities(WorkflowExecution execution)
    {
        if (!execution.AgentOutputs.TryGetValue(
                IngestionWorkflowConstants.MetadataLinkingOutputKey, out string? raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("entities", out var entities) &&
                entities.ValueKind == JsonValueKind.Array)
            {
                return entities.GetArrayLength();
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return 0;
    }

    private static int CountLinks(WorkflowExecution execution)
    {
        if (!execution.AgentOutputs.TryGetValue(
                IngestionWorkflowConstants.MetadataLinkingOutputKey, out string? raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("links", out var links) &&
                links.ValueKind == JsonValueKind.Array)
            {
                return links.GetArrayLength();
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return 0;
    }
}
