using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPC.VCO.DST.Application.Exceptions;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Models.Optimizer;
using MPC.VCO.DST.Infrastructure.Data.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

namespace MPC.VCO.DST.Infrastructure.Repositories;

internal sealed class ForecastRepository(
    dstSqlDbContext context,
    ILogger<ForecastRepository> logger)
    : IForecastRepository
{
    private const string EthanePriceTable = "FORECAST_ETHANE_PRICE";
    private const string NaturalGasPriceTable = "FORECAST_NATURAL_GAS_PRICE";
    private const string InletGasTable = "FORECAST_PLANT_INLET_GAS_VOLUME";
    private const string InletCompositionTable = "FORECAST_PLANT_INLET_C2_COMPOSITION";
    private const string EthaneTakeawayTable = "FORECAST_ETHANE_TAKEAWAY_CAPACITY";
    private const string ResidueTakeawayTable = "FORECAST_RESIDUE_GAS_TAKEAWAY_CAPACITY";
    private static readonly string[] Plants = Enumerable.Range(1, 9)
        .Select(number => $"P{number}")
        .ToArray();

    private readonly dstSqlDbContext _context = context;
    private readonly ILogger<ForecastRepository> _logger = logger;

    public async Task<Forecast> GetForecastAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "End date must be on or after start date.");
        }

        _logger.LogInformation(
            "Loading optimizer forecasts for {StartDate} through {EndDate}.",
            startDate,
            endDate);

        try
        {
            double[] ethanePrice = await LoadScalarSeriesAsync(
                _context.ForecastEthanePrices.AsNoTracking().Select(row => new ForecastObservation
                {
                    Date = row.Date,
                    Value = row.Value
                }),
                EthanePriceTable,
                startDate,
                endDate,
                ForecastValueKind.MarketPrice,
                cancellationToken);
            double[] naturalGasPrice = await LoadScalarSeriesAsync(
                _context.ForecastNaturalGasPrices.AsNoTracking().Select(row => new ForecastObservation
                {
                    Date = row.Date,
                    Value = row.Value
                }),
                NaturalGasPriceTable,
                startDate,
                endDate,
                ForecastValueKind.MarketPrice,
                cancellationToken);
            double[,] inletGas = await LoadPlantSeriesAsync(
                _context.ForecastPlantInletGasVolumes.AsNoTracking().Select(row => new ForecastObservation
                {
                    Date = row.Date,
                    Plant = row.PlantId,
                    Value = row.Value
                }),
                InletGasTable,
                startDate,
                endDate,
                ForecastValueKind.NonNegative,
                cancellationToken);
            double[,] inletComposition = await LoadPlantSeriesAsync(
                _context.ForecastPlantInletC2Compositions.AsNoTracking().Select(row => new ForecastObservation
                {
                    Date = row.Date,
                    Plant = row.PlantId,
                    Value = row.Value
                }),
                InletCompositionTable,
                startDate,
                endDate,
                ForecastValueKind.Fraction,
                cancellationToken);
            double[,] ethaneTakeaway = await LoadPlantSeriesAsync(
                _context.ForecastEthaneTakeawayCapacities.AsNoTracking().Select(row => new ForecastObservation
                {
                    Date = row.Date,
                    Plant = row.PoolId,
                    Value = row.Value
                }),
                EthaneTakeawayTable,
                startDate,
                endDate,
                ForecastValueKind.NonNegative,
                cancellationToken);
            double[,] residueTakeaway = await LoadPlantSeriesAsync(
                _context.ForecastResidueGasTakeawayCapacities.AsNoTracking().Select(row => new ForecastObservation
                {
                    Date = row.Date,
                    Plant = row.PoolId,
                    Value = row.Value
                }),
                ResidueTakeawayTable,
                startDate,
                endDate,
                ForecastValueKind.NonNegative,
                cancellationToken);

            return Forecast.Create(
                startDate,
                endDate,
                ethanePrice,
                naturalGasPrice,
                inletGas,
                inletComposition,
                ethaneTakeaway,
                residueTakeaway);
        }
        catch (ForecastUnavailableException)
        {
            throw;
        }
        catch (DbException exception)
        {
            throw new ForecastUnavailableException("Forecast retrieval failed.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ForecastUnavailableException("Forecast retrieval failed.", exception);
        }
    }

    private async Task<double[]> LoadScalarSeriesAsync(
        IQueryable<ForecastObservation> source,
        string tableName,
        DateOnly startDate,
        DateOnly endDate,
        ForecastValueKind valueKind,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<ForecastObservation> rows = await LoadRelevantScalarRowsAsync(
            source,
            startDate,
            endDate,
            cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "Loaded {RowCount} rows from {TableName} for {StartDate} through {EndDate} in {ElapsedMilliseconds} ms.",
            rows.Count,
            tableName,
            startDate,
            endDate,
            stopwatch.ElapsedMilliseconds);

        ValidateRows(tableName, rows, valueKind);
        if (rows.Count == 0)
        {
            throw MissingForecast(tableName, null);
        }

        return Interpolate(rows, null, tableName, startDate, endDate, valueKind);
    }

    private async Task<double[,]> LoadPlantSeriesAsync(
        IQueryable<ForecastObservation> source,
        string tableName,
        DateOnly startDate,
        DateOnly endDate,
        ForecastValueKind valueKind,
        CancellationToken cancellationToken)
    {
        List<ForecastObservation> rows = await LoadRelevantPlantRowsAsync(
            source,
            startDate,
            endDate,
            cancellationToken);
        foreach (ForecastObservation row in rows)
        {
            row.Plant = NormalizePlantId(row.Plant);
        }
        ValidateRows(tableName, rows, valueKind, plantLevel: true);

        int horizon = endDate.DayNumber - startDate.DayNumber + 1;
        double[,] result = new double[Plants.Length, horizon];
        for (int plantIndex = 0; plantIndex < Plants.Length; plantIndex++)
        {
            string plant = Plants[plantIndex];`
            List<ForecastObservation> plantRows = rows
                .Where(row => string.Equals(row.Plant, plant, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (plantRows.Count == 0)
            {
                throw MissingForecast(tableName, plant);
            }

            double[] daily = Interpolate(plantRows, plant, tableName, startDate, endDate, valueKind);
            for (int day = 0; day < daily.Length; day++)
            {
                result[plantIndex, day] = daily[day];
            }
        }

        return result;
    }

    private async Task<List<ForecastObservation>> LoadRelevantScalarRowsAsync(
        IQueryable<ForecastObservation> source,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        IQueryable<ForecastObservation> relevantRows = source.Where(row =>
            (row.Date >= startDate && row.Date <= endDate)
            || row.Date == source
                .Where(inner => inner.Date < startDate)
                .Select(inner => (DateOnly?)inner.Date)
                .Max()
            || row.Date == source
                .Where(inner => inner.Date > endDate)
                .Select(inner => (DateOnly?)inner.Date)
                .Min());

        return await relevantRows.ToListAsync(cancellationToken);
    }

    private async Task<List<ForecastObservation>> LoadRelevantPlantRowsAsync(
        IQueryable<ForecastObservation> source,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        List<ForecastObservation> rows = await source
            .Where(row => row.Date >= startDate && row.Date <= endDate)
            .ToListAsync(cancellationToken);

        IQueryable<ForecastObservation> priorDates = source
            .Where(row => row.Date < startDate)
            .GroupBy(row => row.Plant)
            .Select(group => new ForecastObservation
            {
                Plant = group.Key,
                Date = group.Max(row => row.Date)
            });
        rows.AddRange(await (
            from row in source
            join prior in priorDates on new { row.Plant, row.Date } equals new { prior.Plant, prior.Date }
            select row).ToListAsync(cancellationToken));

        IQueryable<ForecastObservation> nextDates = source
            .Where(row => row.Date > endDate)
            .GroupBy(row => row.Plant)
            .Select(group => new ForecastObservation
            {
                Plant = group.Key,
                Date = group.Min(row => row.Date)
            });
        rows.AddRange(await (
            from row in source
            join next in nextDates on new { row.Plant, row.Date } equals new { next.Plant, next.Date }
            select row).ToListAsync(cancellationToken));

        return rows;
    }

    internal double[] Interpolate(
        IReadOnlyList<ForecastObservation> observations,
        string? plant,
        string tableName,
        DateOnly startDate,
        DateOnly endDate,
        ForecastValueKind valueKind)
    {
        ForecastObservation[] ordered = observations
            .GroupBy(row => row.Date)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                if (group.Count() > 1)
                {
                    _logger.LogWarning(
                        "Forecast table {TableName} contains {RowCount} rows for {ForecastDate} and {Plant}; averaging duplicate forecasts.",
                        tableName,
                        group.Count(),
                        group.Key,
                        plant);
                }

                double value = group.Average(row => row.Value);
                ValidateValue(tableName, value, $"averaged date {group.Key:yyyy-MM-dd}", valueKind);
                return new ForecastObservation { Date = group.Key, Plant = plant, Value = value };
            })
            .ToArray();

        int horizon = endDate.DayNumber - startDate.DayNumber + 1;
        double[] values = new double[horizon];
        int missingCount = 0;
        for (int day = 0; day < horizon; day++)
        {
            DateOnly date = startDate.AddDays(day);
            int after = Array.FindIndex(ordered, row => row.Date >= date);
            if (after >= 0 && ordered[after].Date == date)
            {
                values[day] = ordered[after].Value;
                continue;
            }

            missingCount++;
            int before = after < 0 ? ordered.Length - 1 : after - 1;
            if (before < 0)
            {
                values[day] = ordered[after].Value;
                continue;
            }
            if (after < 0)
            {
                values[day] = ordered[before].Value;
                continue;
            }

            int span = ordered[after].Date.DayNumber - ordered[before].Date.DayNumber;
            int elapsed = date.DayNumber - ordered[before].Date.DayNumber;
            double fraction = (double)elapsed / span;
            values[day] = ordered[before].Value + fraction * (ordered[after].Value - ordered[before].Value);
            ValidateValue(tableName, values[day], $"interpolated date {date:yyyy-MM-dd}", valueKind);
        }

        if (missingCount > 0)
        {
            _logger.LogWarning(
                "Forecast table {TableName} is missing {MissingCount} daily observations for {StartDate} through {EndDate} and {Plant}; interpolated gaps and extended nearest boundary values.",
                tableName,
                missingCount,
                startDate,
                endDate,
                plant);
        }

        return values;
    }

    internal static void ValidateRows(
        string tableName,
        IReadOnlyList<ForecastObservation> rows,
        ForecastValueKind valueKind,
        bool plantLevel = false)
    {
        HashSet<string> plants = new(Plants, StringComparer.OrdinalIgnoreCase);
        foreach (ForecastObservation row in rows)
        {
            ValidateValue(tableName, row.Value, $"date {row.Date:yyyy-MM-dd}", valueKind);
            if (plantLevel && (string.IsNullOrWhiteSpace(row.Plant) || !plants.Contains(row.Plant)))
            {
                throw new ForecastUnavailableException(
                    $"Forecast table '{tableName}' contains an invalid plant or pool identifier '{row.Plant}'.");
            }
        }
    }

    private static void ValidateValue(
        string tableName,
        double value,
        string source,
        ForecastValueKind valueKind)
    {
        if (!double.IsFinite(value))
        {
            throw new ForecastUnavailableException($"Forecast table '{tableName}' has a non-finite value at {source}.");
        }
        if (valueKind == ForecastValueKind.NonNegative && value < 0)
        {
            throw new ForecastUnavailableException($"Forecast table '{tableName}' has a negative value at {source}.");
        }
        if (valueKind == ForecastValueKind.Fraction && value is < 0 or > 1)
        {
            throw new ForecastUnavailableException($"Forecast table '{tableName}' must contain ethane mole fractions from 0 through 1 at {source}.");
        }
    }

    private static ForecastUnavailableException MissingForecast(string tableName, string? plant) =>
        new(plant is null
            ? $"Forecast table '{tableName}' contains no usable records for the planning range."
            : $"Forecast table '{tableName}' has no records for plant '{plant}'.");

    internal static string NormalizePlantId(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        string normalized = identifier.Trim().ToUpperInvariant();

        string numberPart = normalized.StartsWith("PLANT_", StringComparison.Ordinal)
            ? normalized[6..]
            : normalized.StartsWith("POOL_", StringComparison.Ordinal)
                ? normalized[5..]
                : normalized.StartsWith('P')
                    ? normalized[1..]
                    : string.Empty;

        if (int.TryParse(numberPart, out int number) && number is >= 1 and <= 9)
        {
            return $"P{number}";
        }

        return normalized;
    }

    internal enum ForecastValueKind
    {
        MarketPrice,
        NonNegative,
        Fraction
    }

    internal sealed class ForecastObservation
    {
        public DateOnly Date { get; set; }
        public string? Plant { get; set; }
        public double Value { get; set; }
    }
}
