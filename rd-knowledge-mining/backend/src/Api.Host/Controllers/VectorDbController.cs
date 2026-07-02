using CohereRndKnowledgeMining.Api.Host.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CohereRndKnowledgeMining.Api.Host.Controllers;

[ApiController]
[Route("api/rd-knowledge")]
public sealed class VectorDbController : ControllerBase
{
    [HttpGet("vector-db/summary")]
    public ActionResult<VectorDbStoreSummaryResponse> GetSummary() =>
        Ok(new VectorDbStoreSummaryResponse
        {
            TotalStudies = 0,
            TotalDocuments = 0,
            TotalEntities = 0,
            TotalLinks = 0,
            LastIngestionAt = null,
            LastIngestedStudyId = null
        });
}
