using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using Microsoft.AspNetCore.Authorization;
using MPC.VCO.DST.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MPC.VCO.DST.WebSite.API.Controllers;

[ApiController]
[Authorize]
[Route("api/planning-cycles")]
public sealed class PlanningCyclesController(
    IPlanningCycleService planningCycleService)
    : ControllerBase
{
    private readonly IPlanningCycleService _planningCycleService = planningCycleService;

    /// <summary>
    /// Gets planning cycles for the planning-cycle navigation.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<PlanningCycleResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<
        IReadOnlyList<PlanningCycleResponse>>>
        GetPlanningCyclesAsync(
            [FromQuery] int year,
            [FromQuery] int month,
            CancellationToken cancellationToken)
    {
        var request = new GetPlanningCyclesRequest
        {
            Year = year,
            Month = month,
        };

        var response =
            await _planningCycleService
                .GetPlanningCyclesAsync(
                    request,
                    cancellationToken);

        return Ok(response);
    }
}