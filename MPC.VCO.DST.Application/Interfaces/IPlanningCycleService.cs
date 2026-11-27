using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;

namespace MPC.VCO.DST.Application.Interfaces;

public interface IPlanningCycleService
{
    Task<IReadOnlyList<PlanningCycleResponse>>
        GetPlanningCyclesAsync(
            GetPlanningCyclesRequest request,
            CancellationToken cancellationToken = default);
}