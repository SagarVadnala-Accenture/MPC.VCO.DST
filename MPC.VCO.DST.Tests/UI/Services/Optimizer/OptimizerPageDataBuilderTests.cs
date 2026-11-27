using MudBlazor;
using MPC.VCO.DST.WebSite.Services.Optimizer;
using MPC.VCO.DST.WebSite.Services.Optimizer.Models;

namespace MPC.VCO.DST.Tests.UI.Services.Optimizer;

[TestClass]
public sealed class OptimizerPageDataBuilderTests
{
    [TestMethod]
    public void CalculateInclusiveHorizon_ValidDates_ReturnsInclusiveDayCount()
    {
        int? horizon = OptimizerPageDataBuilder.CalculateInclusiveHorizon(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 30));

        Assert.AreEqual(30, horizon);
    }

    [TestMethod]
    public void BuildPlantRecoveryGrid_DuplicateDayValues_UsesFirstOccurrencePerPlantDay()
    {
        PlantDayDto[] source =
        [
            new PlantDayDto { Plant = "P1", Day = 1, Recovery = 0.10 },
            new PlantDayDto { Plant = "P1", Day = 1, Recovery = 0.30 },
            new PlantDayDto { Plant = "P1", Day = 2, Recovery = 0.50 },
            new PlantDayDto { Plant = "P2", Day = 1, Recovery = 0.20 }
        ];

        IReadOnlyList<PlantRecoveryGridRow> rows = OptimizerPageDataBuilder.BuildPlantRecoveryGrid(source);

        Assert.AreEqual(2, rows.Count);
        PlantRecoveryGridRow p1 = rows.Single(row => row.Plant == "P1");
        Assert.AreEqual(0.10, p1.Recoveries[1], 0.00001);
        Assert.AreEqual(0.50, p1.Recoveries[2], 0.00001);
        Assert.AreEqual(0.30, p1.AverageRecovery ?? 0, 0.00001);
    }

    [TestMethod]
    public void BuildPlantRecoverySeries_MissingPlantDay_FillsNaNForMissingPoint()
    {
        PlantDayDto[] source =
        [
            new PlantDayDto { Plant = "P1", Day = 1, Recovery = 0.10 },
            new PlantDayDto { Plant = "P1", Day = 2, Recovery = 0.20 },
            new PlantDayDto { Plant = "P2", Day = 1, Recovery = 0.30 }
        ];

        List<ChartSeries<double>> series = OptimizerPageDataBuilder.BuildPlantRecoverySeries(source);

        ChartSeries<double> p2 = series.Single(item => item.Name == "P2");
        Assert.AreEqual(2, p2.Data.Count);
        double day1Value = GetPointValue(p2.Data[0]);
        double day2Value = GetPointValue(p2.Data[1]);

        Assert.AreEqual(0.30, day1Value, 0.00001);
        Assert.IsTrue(double.IsNaN(day2Value));
    }

    [TestMethod]
    public void FilterPlantRecoverySeries_SelectedPlant_ReturnsOnlySelectedPlant()
    {
        PlantDayDto[] source =
        [
            new PlantDayDto { Plant = "P1", Day = 1, Recovery = 0.10 },
            new PlantDayDto { Plant = "P2", Day = 1, Recovery = 0.20 }
        ];

        List<ChartSeries<double>> series = OptimizerPageDataBuilder.BuildPlantRecoverySeries(source);

        List<ChartSeries<double>> filtered = OptimizerPageDataBuilder.FilterPlantRecoverySeries(series, "p2");

        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("P2", filtered[0].Name);
    }

    [TestMethod]
    public void GetPlantColor_UnknownPlant_ReturnsDefaultColor()
    {
        string color = OptimizerPageDataBuilder.GetPlantColor("UNKNOWN");

        Assert.AreEqual("var(--primary-color)", color);
    }

    private static double GetPointValue(object point)
    {
        Type pointType = point.GetType();
        string[] candidateProperties = ["Y", "Value", "YValue"];

        foreach (string propertyName in candidateProperties)
        {
            object? value = pointType.GetProperty(propertyName)?.GetValue(point);
            if (value is not null)
            {
                return Convert.ToDouble(value);
            }
        }

        throw new InvalidOperationException("Could not read chart point value from MudBlazor chart data point.");
    }
}
