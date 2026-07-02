using CohereRndKnowledgeMining.Api.Host.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CohereRndKnowledgeMining.Api.Host.Controllers;

[ApiController]
[Route("api/rd-knowledge")]
public sealed class VectorDbController : ControllerBase
{
    [HttpGet("vector-db/summary")]
    public ActionResult<VectorDbStoreSummaryDto> GetSummary() =>
        Ok(new VectorDbStoreSummaryDto
        {
            TotalStudies = 0,
            TotalDocuments = 0,
            TotalEntities = 0,
            TotalLinks = 0,
            LastIngestionAt = null,
            LastIngestedStudyId = null
        });
}
