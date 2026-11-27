using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MPC.VCO.DST.WebSite.API.Controllers;

[ApiController]
[Authorize]
[Route("api/planning-cycles/{planningCycleId}/models")]
public sealed class ModelsController(
    IModelService modelService)
    : ControllerBase
{
    private readonly IModelService _modelService = modelService;

    /// <summary>
    /// Gets models belonging to a planning cycle.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<ModelResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<
        IReadOnlyList<ModelResponse>>>
        GetModelsAsync(
            [FromRoute] string planningCycleId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
    {
        var request = new GetModelsRequest
        {
            PlanningCycleId = planningCycleId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var response =
            await _modelService.GetModelsAsync(
                request,
                cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific model.
    /// </summary>
    [HttpGet("{modelId}")]
    [ProducesResponseType(
        typeof(ModelResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModelResponse>>
        GetModelAsync(
            [FromRoute] string planningCycleId,
            [FromRoute] string modelId,
            CancellationToken cancellationToken)
    {
        var response =
            await _modelService.GetModelAsync(
                planningCycleId,
                modelId,
                cancellationToken);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(ModelResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModelResponse>>
        CreateModelAsync(
            [FromRoute] string planningCycleId,
            [FromBody] CreateModelRequest request,
            CancellationToken cancellationToken)
    {
        var createRequest = new CreateModelRequest
        {
            PlanningCycleId = planningCycleId,
            Status = request.Status,
            Scope = request.Scope,
            ModelType = request.ModelType,
            EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
            NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu
        };

        ModelResponse response =
            await _modelService.CreateModelAsync(
                createRequest,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetModelAsync),
            new
            {
                planningCycleId,
                modelId = response.ModelId
            },
            response);
    }

    [HttpPut("{modelId}")]
    [ProducesResponseType(
        typeof(ModelResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModelResponse>>
        UpdateModelAsync(
            [FromRoute] string planningCycleId,
            [FromRoute] string modelId,
            [FromBody] UpdateModelRequest request,
            CancellationToken cancellationToken)
    {
        var updateRequest = new UpdateModelRequest
        {
            PlanningCycleId = planningCycleId,
            ModelId = modelId,
            Status = request.Status,
            Scope = request.Scope,
            ModelType = request.ModelType,
            EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
            NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu
        };

        ModelResponse? response =
            await _modelService.UpdateModelAsync(
                updateRequest,
                cancellationToken);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpDelete("{modelId}")]
    [ProducesResponseType(
        StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult>
        DeleteModelAsync(
            [FromRoute] string planningCycleId,
            [FromRoute] string modelId,
            CancellationToken cancellationToken)
    {
        bool deleted =
            await _modelService.DeleteModelAsync(
                planningCycleId,
                modelId,
                cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}