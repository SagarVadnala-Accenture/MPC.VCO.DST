using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services;

public static class PlanningCycleMapper
{
    public static PlanningCycle Map(
        DateOnly period,
        IReadOnlyList<MPC.VCO.DST.WebSite.Models.PlanningCycleApiResponse> planningCycles)
    {
        ArgumentNullException.ThrowIfNull(planningCycles);

        MPC.VCO.DST.WebSite.Models.PlanningCycleApiResponse? cycle =
            planningCycles.FirstOrDefault(
                item => item.Year == period.Year && item.Month == period.Month);

        if (cycle is null)
        {
            return new PlanningCycle(string.Empty, period, 0, null, null, null);
        }

        string? bestModelId =
            cycle.BestModel is null || string.IsNullOrWhiteSpace(cycle.BestModel.ModelId)
                ? null
                : cycle.BestModel.ModelId;

        int? recovery = null;

        int? expectedValue =
            cycle.BestModel is null
                ? null
                : (int?)Math.Round(
                    cycle.BestModel.ExpectedValueMonthlyUsd / 1000m,
                    MidpointRounding.AwayFromZero);

        return new PlanningCycle(
            cycle.PlanningCycleId,
            period,
            0,
            bestModelId,
            recovery,
            expectedValue);
    }
}
