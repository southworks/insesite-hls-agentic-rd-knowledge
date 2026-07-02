using CohereRndKnowledgeMining.Api.Host.Contracts;
using CohereRndKnowledgeMining.Api.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace CohereRndKnowledgeMining.Api.Host.Controllers;

/// <summary>
/// Block 2 (Query) endpoints consumed by the Blazor frontend.
/// Process 1: Search &amp; Chat. Process 2: Curate with Compliance Reviewer gate.
/// </summary>
[ApiController]
[Route("api/rd-knowledge")]
public sealed class QueryWorkflowController : ControllerBase
{
    private readonly QueryWorkflowService _queryService;
    private readonly ILogger<QueryWorkflowController> _logger;

    public QueryWorkflowController(
        QueryWorkflowService queryService,
        ILogger<QueryWorkflowController> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    [HttpPost("query/sessions/{sessionId}/workflow/start")]
    public ActionResult<StartQueryWorkflowResponseDto> StartQueryWorkflow(string sessionId)
    {
        string executionId = $"qry-{Guid.NewGuid():N}"[..12];

        try
        {
            return Ok(_queryService.StartQueryWorkflow(sessionId, executionId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetailsResponse
            {
                Title = "Query workflow cannot be started.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("executions/{executionId}/query/session")]
    public ActionResult<QuerySessionStateDto> GetQuerySession(string executionId)
    {
        try
        {
            return Ok(_queryService.GetQuerySession(executionId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Query execution not found.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("executions/{executionId}/query/chat")]
    public async Task<ActionResult<QuerySessionStateDto>> SendChatMessage(
        string executionId,
        [FromBody] SendChatMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            QuerySessionStateDto response = await _queryService.SendChatMessageAsync(
                executionId,
                request.Question,
                request.StudyScope,
                cancellationToken);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Query execution not found.",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetailsResponse
            {
                Title = "Search & Chat request is invalid.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search & Chat failed for execution {ExecutionId}.", executionId);

            return Problem(
                detail: ex.Message,
                title: "Search & Chat failed.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("executions/{executionId}/query/curate")]
    public ActionResult<StartCurationResponseDto> StartCuration(string executionId)
    {
        try
        {
            return Ok(_queryService.StartCurationWorkflow(executionId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Query execution not found.",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetailsResponse
            {
                Title = "Curate workflow cannot be started.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("executions/{executionId}/query/status")]
    public ActionResult<CurationWorkflowProgressDto> GetCurationStatus(string executionId)
    {
        try
        {
            return Ok(_queryService.GetCurationProgress(executionId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Query execution not found.",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetailsResponse
            {
                Title = "Curation has not started.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("executions/{executionId}/query/resume")]
    public ActionResult<SubmitQueryDecisionResponseDto> SubmitCurationDecision(
        string executionId,
        [FromBody] SubmitQueryDecisionRequestDto request)
    {
        try
        {
            SubmitQueryDecisionResponseDto response = _queryService.SubmitCurationDecision(
                executionId,
                request.Approved,
                request.Notes);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Query execution not found.",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetailsResponse
            {
                Title = "Curate workflow cannot be resumed.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume curate workflow for execution {ExecutionId}.", executionId);

            return Problem(
                detail: ex.Message,
                title: "Curate workflow failed to resume.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
