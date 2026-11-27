namespace MPC.VCO.DST.Application.DTOs.Response;

public sealed class PlanningCycleResponse
{
    public string PlanningCycleId { get; init; }
        = string.Empty;

    public int Year { get; init; }

    public int Month { get; init; }

    public string Name { get; init; }       
        = string.Empty;

    public string CycleState { get; init; }
        = string.Empty;

    public BestModelResponse? BestModel { get; init; }
}