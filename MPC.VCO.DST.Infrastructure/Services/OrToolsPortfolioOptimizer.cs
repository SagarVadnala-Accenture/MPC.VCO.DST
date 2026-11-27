using Google.OrTools.LinearSolver;
using Microsoft.Extensions.Logging;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Models.Optimizer;
using System.Diagnostics;

namespace MPC.VCO.DST.Infrastructure.Services;

internal sealed class OrToolsPortfolioOptimizer(
    ILogger<OrToolsPortfolioOptimizer> logger)
    : IPortfolioOptimizer
{
    private const double Infinity = double.PositiveInfinity;
    private readonly ILogger<OrToolsPortfolioOptimizer> _logger = logger;

    public Task<PortfolioOptimizationResult> SolveAsync(
        OptimizeRequest request,
        Forecast forecast,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(forecast);
        cancellationToken.ThrowIfCancellationRequested();

        string[] trains = forecast.Trains;
        int[] days = forecast.Days;
        int trainCount = trains.Length;
        int dayCount = days.Length;
        double[,] inletEthaneBbl = forecast.InletEthaneBbl;

        TrainSettings defaults = new();
        TrainSettings[] settings = trains.Select(train => request.Trains?
                .Where(pair => string.Equals(pair.Key, train, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value)
                .FirstOrDefault()
            ?? defaults)
            .ToArray();

        double[] ethanePrice = forecast.EthanePrice
            .Select(price => price * (1 + request.EthanePriceShiftPct / 100))
            .ToArray();
        double[] gasPrice = forecast.GasPrice
            .Select(price => price * (1 + request.GasPriceShiftPct / 100))
            .ToArray();
        double[] portfolioTakeaway = new double[dayCount];
        for (int day = 0; day < dayCount; day++)
        {
            double totalInletEthane = 0;
            for (int train = 0; train < trainCount; train++)
            {
                totalInletEthane += inletEthaneBbl[train, day];
            }
            portfolioTakeaway[day] = request.TakeawayBblPerDay ?? 0.40 * totalInletEthane;
        }

        using Solver solver = Solver.CreateSolver("CBC")
            ?? Solver.CreateSolver("SCIP")
            ?? throw new InvalidOperationException("OR-Tools build has neither CBC nor SCIP.");

        Variable[,] recovery = new Variable[trainCount, dayCount];
        Variable[,] changeSize = new Variable[trainCount, dayCount];
        Variable[,] changeFlag = new Variable[trainCount, dayCount];
        Objective objective = solver.Objective();
        objective.SetMaximization();

        for (int trainIndex = 0; trainIndex < trainCount; trainIndex++)
        {
            string train = trains[trainIndex];
            TrainSettings trainSettings = settings[trainIndex];
            (double minimum, double maximum) = trainSettings.LockedAt is double lockedAt
                ? (lockedAt, lockedAt)
                : (trainSettings.RMin, trainSettings.RMax);
            double bigM = Math.Max(
                1e-6,
                Math.Max(maximum, trainSettings.R0) - Math.Min(minimum, trainSettings.R0));

            for (int dayIndex = 0; dayIndex < dayCount; dayIndex++)
            {
                int day = days[dayIndex];
                recovery[trainIndex, dayIndex] = solver.MakeNumVar(minimum, maximum, $"r[{train},{day}]");
                changeSize[trainIndex, dayIndex] = solver.MakeNumVar(0, Infinity, $"d[{train},{day}]");
                changeFlag[trainIndex, dayIndex] = solver.MakeBoolVar($"z[{train},{day}]");

                Constraint upperAbsoluteChange = solver.MakeConstraint(
                    dayIndex == 0 ? -trainSettings.R0 : 0,
                    Infinity,
                    $"dUp[{train},{day}]");
                Constraint lowerAbsoluteChange = solver.MakeConstraint(
                    dayIndex == 0 ? trainSettings.R0 : 0,
                    Infinity,
                    $"dDn[{train},{day}]");
                upperAbsoluteChange.SetCoefficient(changeSize[trainIndex, dayIndex], 1);
                upperAbsoluteChange.SetCoefficient(recovery[trainIndex, dayIndex], -1);
                lowerAbsoluteChange.SetCoefficient(changeSize[trainIndex, dayIndex], 1);
                lowerAbsoluteChange.SetCoefficient(recovery[trainIndex, dayIndex], 1);
                if (dayIndex > 0)
                {
                    upperAbsoluteChange.SetCoefficient(recovery[trainIndex, dayIndex - 1], 1);
                    lowerAbsoluteChange.SetCoefficient(recovery[trainIndex, dayIndex - 1], -1);
                }

                Constraint changeLink = solver.MakeConstraint(-Infinity, 0, $"link[{train},{day}]");
                changeLink.SetCoefficient(changeSize[trainIndex, dayIndex], 1);
                changeLink.SetCoefficient(changeFlag[trainIndex, dayIndex], -bigM);

                double unitMargin = ethanePrice[dayIndex]
                    - request.TfFee
                    - request.EthaneHeatingValue * gasPrice[dayIndex]
                    - trainSettings.COp;
                objective.SetCoefficient(recovery[trainIndex, dayIndex], inletEthaneBbl[trainIndex, dayIndex] * unitMargin);
                objective.SetCoefficient(changeFlag[trainIndex, dayIndex], -request.KFix);
                objective.SetCoefficient(changeSize[trainIndex, dayIndex], -request.KVar);
            }

            Constraint changeBudget = solver.MakeConstraint(-Infinity, request.MaxChangesPerTrain, $"nchg[{train}]");
            for (int dayIndex = 0; dayIndex < dayCount; dayIndex++)
            {
                changeBudget.SetCoefficient(changeFlag[trainIndex, dayIndex], 1);
            }

            if (request.MinHoldDays > 1)
            {
                for (int startIndex = 0; startIndex < dayCount; startIndex++)
                {
                    Constraint holdWindow = solver.MakeConstraint(-Infinity, 1, $"hold[{train},{days[startIndex]}]");
                    for (int index = startIndex;
                         index < dayCount && days[index] < days[startIndex] + request.MinHoldDays;
                         index++)
                    {
                        holdWindow.SetCoefficient(changeFlag[trainIndex, index], 1);
                    }
                }
            }
        }

        for (int dayIndex = 0; dayIndex < dayCount; dayIndex++)
        {
            Constraint portfolioCapacity = solver.MakeConstraint(-Infinity, portfolioTakeaway[dayIndex], $"takeaway[{days[dayIndex]}]");
            for (int trainIndex = 0; trainIndex < trainCount; trainIndex++)
            {
                portfolioCapacity.SetCoefficient(recovery[trainIndex, dayIndex], inletEthaneBbl[trainIndex, dayIndex]);
            }
        }

        Dictionary<string, int> plantIndices = forecast.Plants
            .Select((plant, index) => (plant, index))
            .ToDictionary(item => item.plant, item => item.index, StringComparer.Ordinal);
        for (int plantIndex = 0; plantIndex < forecast.Plants.Length; plantIndex++)
        {
            string plant = forecast.Plants[plantIndex];
            for (int dayIndex = 0; dayIndex < dayCount; dayIndex++)
            {
                int day = days[dayIndex];
                Constraint ethaneCapacity = solver.MakeConstraint(
                    -Infinity,
                    forecast.PlantEthaneTakeawayBblPerDay[plantIndex, dayIndex],
                    $"plantEthaneTakeaway[{plant},{day}]");
                Constraint residueCapacity = solver.MakeConstraint(
                    (forecast.PlantInletGasVolumeMmscfPerDay[plantIndex, dayIndex]
                     - forecast.PlantResidueGasTakeawayMmscfPerDay[plantIndex, dayIndex])
                    * Forecast.EthaneBblPerMmscfAtOneMoleFraction,
                    Infinity,
                    $"plantResidueTakeaway[{plant},{day}]");

                for (int trainIndex = 0; trainIndex < trainCount; trainIndex++)
                {
                    if (plantIndices[forecast.TrainPlant[trains[trainIndex]]] == plantIndex)
                    {
                        ethaneCapacity.SetCoefficient(recovery[trainIndex, dayIndex], inletEthaneBbl[trainIndex, dayIndex]);
                        residueCapacity.SetCoefficient(recovery[trainIndex, dayIndex], inletEthaneBbl[trainIndex, dayIndex]);
                    }
                }
            }
        }

        solver.SetTimeLimit(request.TimeLimitSec * 1000L);
        using MPSolverParameters parameters = new();
        parameters.SetDoubleParam(MPSolverParameters.DoubleParam.RELATIVE_MIP_GAP, request.MipGap);

        Stopwatch stopwatch = Stopwatch.StartNew();
        Solver.ResultStatus solverStatus;
        using (cancellationToken.Register(() => solver.InterruptSolve()))
        {
            solverStatus = solver.Solve(parameters);
        }
        stopwatch.Stop();
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Portfolio optimization was interrupted after {DurationMilliseconds} ms.",
                stopwatch.ElapsedMilliseconds);
            cancellationToken.ThrowIfCancellationRequested();
        }

        string status = solverStatus.ToString().ToLowerInvariant();
        _logger.LogInformation(
            "Portfolio optimization solver returned {SolverStatus} in {DurationMilliseconds} ms.",
            status,
            stopwatch.ElapsedMilliseconds);

        if (solverStatus is not (Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE))
        {
            _logger.LogWarning("Portfolio optimization has no feasible incumbent. Solver status: {SolverStatus}.", status);
            return Task.FromResult(new PortfolioOptimizationResult(status, null, [], []));
        }

        List<TrainDay> schedule = [];
        SortedDictionary<(string Plant, int Day), (double Ethane, double Inlet)> plantTotals = [];
        for (int trainIndex = 0; trainIndex < trainCount; trainIndex++)
        {
            string plant = forecast.TrainPlant[trains[trainIndex]];
            double previous = settings[trainIndex].R0;
            for (int dayIndex = 0; dayIndex < dayCount; dayIndex++)
            {
                double value = recovery[trainIndex, dayIndex].SolutionValue();
                schedule.Add(new TrainDay
                {
                    Train = trains[trainIndex],
                    Plant = plant,
                    Day = days[dayIndex],
                    Date = forecast.StartDate.AddDays(days[dayIndex] - 1),
                    Recovery = Math.Round(value, 4),
                    Changed = Math.Abs(value - previous) > 1e-6
                });
                previous = value;

                (string Plant, int Day) key = (plant, days[dayIndex]);
                plantTotals.TryGetValue(key, out (double Ethane, double Inlet) total);
                plantTotals[key] = (
                    total.Ethane + inletEthaneBbl[trainIndex, dayIndex] * value,
                    total.Inlet + inletEthaneBbl[trainIndex, dayIndex]);
            }
        }

        List<PlantDay> plantRecovery = plantTotals
            .Select(entry => new PlantDay
            {
                Plant = entry.Key.Plant,
                Day = entry.Key.Day,
                Date = forecast.StartDate.AddDays(entry.Key.Day - 1),
                Recovery = Math.Round(entry.Value.Inlet > 0 ? entry.Value.Ethane / entry.Value.Inlet : 0, 4)
            })
            .ToList();

        return Task.FromResult(new PortfolioOptimizationResult(
            status,
            objective.Value(),
            schedule,
            plantRecovery));
    }
}
