using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Exceptions;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Models.Optimizer;
using Microsoft.Extensions.Logging;

using System.Diagnostics;


namespace MPC.VCO.DST.Application.Implementation;

internal sealed class OptimizerService(
    IForecastRepository forecastRepository,
    IPortfolioOptimizer portfolioOptimizer,
    ILogger<OptimizerService> logger)
    : IOptimizerService
{
    private const int MaximumPlanningDays = 90;

    private readonly IForecastRepository _forecastRepository = forecastRepository;
    private readonly IPortfolioOptimizer _portfolioOptimizer = portfolioOptimizer;
    private readonly ILogger<OptimizerService> _logger = logger;

    public async Task<OptimizeResponse> OptimizeAsync(
        OptimizeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, string[]> errors = ValidateRequest(request);
        if (errors.Count > 0)
        {
            throw new OptimizerValidationException(errors);
        }

        (DateOnly startDate, DateOnly endDate) = request.ResolvePlanningDates(
            DateOnly.FromDateTime(DateTime.UtcNow));
        _logger.LogInformation(
            "Starting optimizer for planning range {StartDate} through {EndDate}.",
            startDate,
            endDate);

        Forecast forecast;
        try
        {
            forecast = await _forecastRepository.GetForecastAsync(
                startDate,
                endDate,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Optimizer forecast retrieval was canceled for {StartDate} through {EndDate}.",
                startDate,
                endDate);
            throw;
        }

        string[] unknownTrains = request.Trains!.Keys
            .Where(train => !forecast.TrainPlant.Keys.Contains(train, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (unknownTrains.Length > 0)
        {
            throw new OptimizerValidationException(new Dictionary<string, string[]>
            {
                ["trains"] = [$"Unknown train id(s): {string.Join(", ", unknownTrains)}"]
            });
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        PortfolioOptimizationResult result;
        try
        {
            result = await _portfolioOptimizer.SolveAsync(
                request,
                forecast,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Optimizer solve was canceled for {StartDate} through {EndDate}.",
                startDate,
                endDate);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }

        _logger.LogInformation(
            "Optimizer completed with status {SolverStatus} in {DurationMilliseconds} ms for {StartDate} through {EndDate}.",
            result.Status,
            stopwatch.ElapsedMilliseconds,
            startDate,
            endDate);

        if (result.MarginUsd is not double margin)
        {
            throw new OptimizerInfeasibleException(result.Status);
        }

        return new OptimizeResponse
        {
            ScenarioId = Guid.NewGuid().ToString(),
            Status = result.Status,
            PortfolioMarginUsd = Math.Round(margin, 2),
            PlanningStartDate = forecast.StartDate,
            PlanningEndDate = forecast.EndDate,
            TrainSchedule = result.TrainSchedule,
            PlantRecovery = result.PlantRecovery
        };
    }

    private static Dictionary<string, string[]> ValidateRequest(OptimizeRequest request)
    {
        Dictionary<string, string[]> errors = [];

        void AddError(string key, string message) => errors[key] = [message];
        static bool IsFraction(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

        if (request.PlanningStartDate.HasValue != request.PlanningEndDate.HasValue)
        {
            AddError("planning_dates", "planning_start_date and planning_end_date must be provided together.");
        }
        else if (request.PlanningStartDate is DateOnly start && request.PlanningEndDate is DateOnly end)
        {
            int days = end.DayNumber - start.DayNumber + 1;
            if (days < 1)
            {
                AddError("planning_end_date", "Must be on or after planning_start_date.");
            }
            else if (days > MaximumPlanningDays)
            {
                AddError("planning_end_date", $"Planning range must not exceed {MaximumPlanningDays} inclusive calendar days.");
            }
        }

        if (request.TimeLimitSec is < 1 or > 220)
        {
            AddError("time_limit_sec", "Must be between 1 and 220.");
        }
        if (!double.IsFinite(request.MipGap) || request.MipGap is < 0 or > 1)
        {
            AddError("mip_gap", "Must be a finite value between 0 and 1.");
        }
        if (request.MaxChangesPerTrain < 0)
        {
            AddError("max_changes_per_train", "Must be 0 or more.");
        }
        if (request.MinHoldDays < 0)
        {
            AddError("min_hold_days", "Must be 0 or more.");
        }
        if (!double.IsFinite(request.EthanePriceShiftPct) || !double.IsFinite(request.GasPriceShiftPct))
        {
            AddError("price_shifts", "Price shifts must be finite numeric values.");
        }
        if (!double.IsFinite(request.TfFee) || request.TfFee < 0)
        {
            AddError("tf_fee", "Must be a finite nonnegative value.");
        }
        if (!double.IsFinite(request.EthaneHeatingValue) || request.EthaneHeatingValue < 0)
        {
            AddError("ethane_heating_value", "Must be a finite nonnegative value.");
        }
        if (!double.IsFinite(request.KFix) || request.KFix < 0)
        {
            AddError("k_fix", "Must be a finite nonnegative value.");
        }
        if (!double.IsFinite(request.KVar) || request.KVar < 0)
        {
            AddError("k_var", "Must be a finite nonnegative value.");
        }
        if (request.TakeawayBblPerDay is double takeaway
            && (!double.IsFinite(takeaway) || takeaway < 0))
        {
            AddError("takeaway_bbl_per_day", "Must be a finite nonnegative value.");
        }

        if (request.Trains is null)
        {
            AddError("trains", "Trains must be an object.");
            return errors;
        }

        foreach ((string trainId, TrainSettings? settings) in request.Trains)
        {
            if (settings is null)
            {
                AddError($"trains.{trainId}", "Train settings are required.");
                continue;
            }

            bool validSettings = IsFraction(settings.RMin)
                && IsFraction(settings.RMax)
                && IsFraction(settings.R0)
                && (settings.LockedAt is null || IsFraction(settings.LockedAt.Value))
                && double.IsFinite(settings.COp)
                && settings.COp >= 0;
            if (!validSettings)
            {
                AddError($"trains.{trainId}", "Recovery fractions must be finite values from 0 through 1 and operating cost must be finite and nonnegative.");
            }
            else if (settings.RMin > settings.RMax)
            {
                AddError($"trains.{trainId}", "r_min must not exceed r_max.");
            }
        }

        return errors;
    }
}
