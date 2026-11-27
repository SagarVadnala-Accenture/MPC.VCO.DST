using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services;

namespace MPC.VCO.DST.Tests.UI.Services;

[TestClass]
public sealed class PlanningCycleTests
{
    [TestMethod]
    public void Title_ValidPeriod_ReturnsMonthAndYearInInvariantCulture()
    {
        var cycle = new PlanningCycle(
            "PC-2026-07",
            new DateOnly(2026, 7, 1),
            0,
            null,
            null,
            null);

        Assert.AreEqual("July, 2026", cycle.Title);
    }

    [TestMethod]
    public void Map_ReturnsBestModelIdAndExpectedValueFromSameBestModel()
    {
        DateOnly period = new(2026, 7, 1);
        var source = new[]
        {
            new PlanningCycleApiResponse
            {
                PlanningCycleId = "PC-2026-07",
                Year = 2026,
                Month = 7,
                Name = "July, 2026",
                CycleState = "Current",
                BestModel = new BestModelApiResponse
                {
                    ModelId = "ID_2026-07-002",
                    ExpectedValueMonthlyUsd = 219_000m
                }
            }
        };

        PlanningCycle mapped = InvokeMap(period, source);

        Assert.AreEqual("ID_2026-07-002", mapped.BestModelId);
        Assert.IsNull(mapped.Recovery);
        Assert.AreEqual(219, mapped.ExpectedValue);
    }

    [TestMethod]
    public void Map_ComputesPerRequestedPlanningCycleOnly()
    {
        var source = new[]
        {
            new PlanningCycleApiResponse
            {
                PlanningCycleId = "PC-2026-07",
                Year = 2026,
                Month = 7,
                Name = "July, 2026",
                CycleState = "Previous",
                BestModel = new BestModelApiResponse
                {
                    ModelId = "ID_2026-07-002",
                    ExpectedValueMonthlyUsd = 219_000m
                }
            },
            new PlanningCycleApiResponse
            {
                PlanningCycleId = "PC-2026-08",
                Year = 2026,
                Month = 8,
                Name = "August, 2026",
                CycleState = "Current",
                BestModel = new BestModelApiResponse
                {
                    ModelId = "ID_2026-08-004",
                    ExpectedValueMonthlyUsd = 247_000m
                }
            }
        };

        PlanningCycle july = InvokeMap(new DateOnly(2026, 7, 1), source);
        PlanningCycle august = InvokeMap(new DateOnly(2026, 8, 1), source);

        Assert.AreEqual("ID_2026-07-002", july.BestModelId); 
        Assert.AreEqual(219, july.ExpectedValue);

        Assert.AreEqual("ID_2026-08-004", august.BestModelId);
        Assert.AreEqual(247, august.ExpectedValue);
    }

    [TestMethod]
    public void Map_UsesSafeNullsWhenBestModelIsMissing()
    {
        DateOnly period = new(2026, 9, 1);
        var source = new[]
        {
            new PlanningCycleApiResponse
            {
                PlanningCycleId = "PC-2026-09",
                Year = 2026,
                Month = 9,
                Name = "September, 2026",
                CycleState = "Next",
                BestModel = null
            }
        };

        PlanningCycle mapped = InvokeMap(period, source);

        Assert.IsNull(mapped.BestModelId);
        Assert.IsNull(mapped.Recovery);
        Assert.IsNull(mapped.ExpectedValue);
    }

    private static PlanningCycle InvokeMap(
        DateOnly period,
        IReadOnlyList<PlanningCycleApiResponse> source)
    {
        return PlanningCycleMapper.Map(period, source);
    }
}
