using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Mappers;
using MPC.VCO.DST.Domain.Interfaces;

namespace MPC.VCO.DST.Application.Implementation;

internal sealed class PlanningCycleService(
    IPlanningCycleRepository planningCycleRepository)
    : IPlanningCycleService
{
    private readonly IPlanningCycleRepository
        _planningCycleRepository =
            planningCycleRepository;

    public async Task<
        IReadOnlyList<PlanningCycleResponse>>
        GetPlanningCyclesAsync(
            GetPlanningCyclesRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Month),
                "Month must be between 1 and 12.");
        }

        var planningCycles =
            await _planningCycleRepository
                .GetPlanningCyclesAsync(
                    request.Year,
                    request.Month,
                    cancellationToken);

        return planningCycles
            .Select(x => x.ToResponse())
            .ToList();
    }
}
