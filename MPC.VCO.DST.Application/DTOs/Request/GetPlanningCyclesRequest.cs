namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class GetPlanningCyclesRequest
{
    public int Year { get; init; }

    public int Month { get; init; }
}