using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;

namespace MPC.VCO.DST.WebSite.API.Controllers;

[ApiController]
[Authorize]
[Route("api/optimizer")]
public sealed class OptimizerController(IOptimizerService optimizerService)
    : ControllerBase
{
    private readonly IOptimizerService _optimizerService = optimizerService;

    [HttpPost]
    [ProducesResponseType(typeof(OptimizeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<OptimizeResponse>> OptimizeAsync(
        [FromBody] OptimizeRequest request,
        CancellationToken cancellationToken)
    {
        OptimizeResponse response = await _optimizerService.OptimizeAsync(request, cancellationToken);
        return Ok(response);
    }
}
