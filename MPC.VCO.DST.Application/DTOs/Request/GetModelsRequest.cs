namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class GetModelsRequest
{
    public string PlanningCycleId { get; init; }
        = string.Empty;

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}