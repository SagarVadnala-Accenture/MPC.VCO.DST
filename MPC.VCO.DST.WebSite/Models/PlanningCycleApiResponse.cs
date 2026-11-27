namespace MPC.VCO.DST.WebSite.Models;

public sealed class PlanningCycleApiResponse
{
    public string PlanningCycleId { get; init; } = string.Empty;

    public int Year { get; init; }

    public int Month { get; init; }

    public string Name { get; init; } = string.Empty;

    public string CycleState { get; init; } = string.Empty;

    public BestModelApiResponse? BestModel { get; init; }
}

public sealed class BestModelApiResponse
{
    public string ModelId { get; init; } = string.Empty;

    //public decimal AverageRecoveryPercent { get; init; }

    public decimal ExpectedValueMonthlyUsd { get; init; }
}
 