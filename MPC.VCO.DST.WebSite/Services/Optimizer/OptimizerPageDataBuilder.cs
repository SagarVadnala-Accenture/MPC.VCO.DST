using System.Globalization;
using MudBlazor;
using MPC.VCO.DST.WebSite.Services.Optimizer.Models;

namespace MPC.VCO.DST.WebSite.Services.Optimizer;

public static class OptimizerPageDataBuilder
{
    private static readonly string[] PlantColors =
    [
        "var(--primary-color)", "var(--primary-muted)", "var(--primary-link)", "var(--primary-deep)", "var(--status-success)",
        "var(--status-error)", "var(--reviewed-text)", "var(--executable-text)", "var(--reconnect-highlight)"
    ];

    public static int? CalculateInclusiveHorizon(DateOnly? startDate, DateOnly? endDate) =>
        startDate is DateOnly start && endDate is DateOnly end
            ? end.DayNumber - start.DayNumber + 1
            : null;

    public static IReadOnlyList<PlantRecoveryGridRow> BuildPlantRecoveryGrid(
        IEnumerable<PlantDayDto> plantRecovery)
    {
        ArgumentNullException.ThrowIfNull(plantRecovery);
        PlantDayDto[] points = plantRecovery.ToArray();
        int[] days = points.Select(point => point.Day).Distinct().Order().ToArray();

        return points
            .GroupBy(point => point.Plant, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                Dictionary<int, double> recoveries = group
                    .GroupBy(point => point.Day)
                    .ToDictionary(day => day.Key, day => day.First().Recovery);

                return new PlantRecoveryGridRow(
                    group.Key,
                    days,
                    recoveries,
                    recoveries.Count == 0 ? null : recoveries.Values.Average());
            })
            .ToArray();
    }

    public static List<ChartSeries<double>> BuildPlantRecoverySeries(
        IEnumerable<PlantDayDto> plantRecovery)
    {
        ArgumentNullException.ThrowIfNull(plantRecovery);
        PlantDayDto[] points = plantRecovery.ToArray();
        int[] days = points.Select(point => point.Day).Distinct().Order().ToArray();

        return points
            .Select(point => point.Plant)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(plant => new ChartSeries<double>
            {
                Name = plant,
                TooltipTitleFormat = "{{SERIES_NAME}}",
                TooltipSubtitleFormat = "{{X_VALUE}}: {{Y_VALUE}}",
                TooltipYValueFormat = "P1",
                Data = days.Select(day => points.FirstOrDefault(point =>
                        point.Day == day
                        && string.Equals(point.Plant, plant, StringComparison.OrdinalIgnoreCase))?.Recovery
                    ?? double.NaN)
                    .ToList()
            })
            .ToList();
    }

    public static List<ChartSeries<double>> FilterPlantRecoverySeries(
        IEnumerable<ChartSeries<double>> series,
        string? selectedPlant)
    {
        ArgumentNullException.ThrowIfNull(series);
        return string.IsNullOrWhiteSpace(selectedPlant)
            ? series.ToList()
            : series.Where(item => string.Equals(
                item.Name, selectedPlant, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static string FormatRecovery(double recovery) =>
        recovery.ToString("P1", CultureInfo.InvariantCulture);

    public static string GetPlantColor(string plant)
    {
        int index = plant.Length > 1
            && plant[0] is 'P' or 'p'
            && int.TryParse(plant.AsSpan(1), out int plantNumber)
            && plantNumber is >= 1 and <= 9
                ? plantNumber - 1
                : 0;
        return PlantColors[index];
    }
}
