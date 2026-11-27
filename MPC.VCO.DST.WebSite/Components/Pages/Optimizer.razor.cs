using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Charts;
using MPC.VCO.DST.WebSite.Services.Optimizer;
using MPC.VCO.DST.WebSite.Services.Optimizer.Models;

namespace MPC.VCO.DST.WebSite.Components.Pages;

public partial class Optimizer : IDisposable
{
    private static readonly string[] VersionNames = OptimizerScenarioStateStore.VersionNames;
    private static readonly OptimizeRequestDto DefaultParameters = new();

    [Inject] private IOptimizerApiClient OptimizerApiClient { get; set; } = default!;
    [Inject] private OptimizerScenarioStateStore ScenarioStateStore { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<Optimizer> Logger { get; set; } = default!;
    [Parameter] public string ModelId { get; set; } = string.Empty;

    private DateTimeOffset? _planningStartDate;
    private DateTimeOffset? _planningEndDate;
    private double _ethanePriceShiftPct;
    private double _gasPriceShiftPct;
    private double _tfFee = DefaultParameters.TfFee;
    private double _ethaneHeatingValue = DefaultParameters.EthaneHeatingValue;
    private double? _takeawayBblPerDay = DefaultParameters.TakeawayBblPerDay;
    private double _kFix = DefaultParameters.KFix;
    private double _kVar = DefaultParameters.KVar;
    private int _maxChangesPerTrain = DefaultParameters.MaxChangesPerTrain;
    private int _minHoldDays = DefaultParameters.MinHoldDays;
    private int _timeLimitSec = DefaultParameters.TimeLimitSec;
    private double _mipGap = DefaultParameters.MipGap;
    private bool _isLoading;
    private int _selectedVersionIndex;
    private OptimizerScenarioState[] _versions = [];
    private OptimizerScenarioState CurrentScenario => _versions[_selectedVersionIndex];
    private string? _loadedModelId;
    private OptimizeResponseDto? _result;
    private OptimizeRequestDto? _completedRequest;
    private CancellationTokenSource? _requestCancellation;
    private string? _errorMessage;
    private string? _apiDetail;
    private IReadOnlyDictionary<string, string[]> _serverValidationErrors =
        new Dictionary<string, string[]>();
    private IReadOnlyList<string> _clientValidationErrors = [];
    private string? _selectedChartPlant;
    private string _plantFilter = string.Empty;
    private string _schedulePlantFilter = "All";
    private string _scheduleTrainFilter = string.Empty;
    private string _scheduleChangedFilter = "All";
    private int? _scheduleDayFilter;
    private List<ChartSeries<double>> _chartSeries = [];

    private int[] RecoveryDays => _result?.PlantRecovery
        .Select(point => point.Day)
        .Distinct()
        .Order()
        .ToArray() ?? [];

    private DateTimeOffset? CompletedAt => CurrentScenario.CompletedAt;

    private DateTime? PlanningStartDatePickerValue
    {
        get => _planningStartDate?.UtcDateTime;
        set => _planningStartDate = value.HasValue
            ? new DateTimeOffset(value.Value, TimeSpan.Zero)
            : null;
    }

    private DateTime? PlanningEndDatePickerValue
    {
        get => _planningEndDate?.UtcDateTime;
        set => _planningEndDate = value.HasValue
            ? new DateTimeOffset(value.Value, TimeSpan.Zero)
            : null;
    }

    private TimeSpan? Elapsed => CurrentScenario.Elapsed;

    private string[] ChartLabels => RecoveryDays
        .Select(FormatDayLabel)
        .ToArray();

    private int? InclusiveHorizon => OptimizerPageDataBuilder.CalculateInclusiveHorizon(
        _planningStartDate is DateTimeOffset start ? DateOnly.FromDateTime(start.UtcDateTime) : null,
        _planningEndDate is DateTimeOffset end ? DateOnly.FromDateTime(end.UtcDateTime) : null);

    private bool IsPlanningPeriodValid => InclusiveHorizon is > 0 and <= 90;

    private int PlanningDuration => InclusiveHorizon ?? 0;

    private List<ChartSeries<double>> VisibleChartSeries =>
        OptimizerPageDataBuilder.FilterPlantRecoverySeries(_chartSeries, _selectedChartPlant);

    private IReadOnlyList<PlantRecoveryGridRow> PlantRecoveryGrid => BuildPlantRecoveryGrid();

    private IReadOnlyList<string> Plants => _result?.PlantRecovery
        .Select(point => point.Plant)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    private IEnumerable<TrainDayDto> FilteredTrainSchedule
    {
        get
        {
            IEnumerable<TrainDayDto> rows = _result?.TrainSchedule ?? [];
            if (!string.Equals(_schedulePlantFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                rows = rows.Where(row => string.Equals(row.Plant, _schedulePlantFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_scheduleTrainFilter))
            {
                rows = rows.Where(row => row.Train.Contains(_scheduleTrainFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (_scheduleChangedFilter == "Changed")
            {
                rows = rows.Where(row => row.Changed);
            }
            else if (_scheduleChangedFilter == "Unchanged")
            {
                rows = rows.Where(row => !row.Changed);
            }

            if (_scheduleDayFilter is int day)
            {
                rows = rows.Where(row => row.Day == day);
            }

            return rows.OrderBy(row => row.Plant, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Train, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Day);
        }
    }

    private LineChartOptions ChartOptions => new()
    {
        XAxisTitle = "Day",
        YAxisTitle = "Recovery",
        LineStrokeWidth = 2,
        ShowDataMarkers = true,
        ChartPalette = VisibleChartSeries.Select(series => GetPlantColor(series.Name)).ToArray(),
        YAxisTicks = 1,
        YAxisRequireZeroPoint = true,
        YAxisSuggestedMax = 1,
        YAxisToStringFunc = value => value.ToString("P0", CultureInfo.InvariantCulture)
    };

    private void RefreshChartSeries()
    {
        _chartSeries = _result is null
            ? []
            : OptimizerPageDataBuilder.BuildPlantRecoverySeries(_result.PlantRecovery);
    }

    private IReadOnlyList<PlantRecoveryGridRow> BuildPlantRecoveryGrid() =>
        _result is null
            ? []
            : OptimizerPageDataBuilder.BuildPlantRecoveryGrid(_result.PlantRecovery);

    private string FormatDayLabel(int day)
    {
        DateOnly? startDate = CurrentScenario.CompletedRequest?.PlanningStartDate;
        return startDate is DateOnly start
            ? start.AddDays(day - 1).ToString("MMM dd", CultureInfo.InvariantCulture)
            : $"Day {day}";
    }

    private static int CalculateInclusiveHorizon(DateTimeOffset start, DateTimeOffset end) =>
        OptimizerPageDataBuilder.CalculateInclusiveHorizon(
            DateOnly.FromDateTime(start.UtcDateTime),
            DateOnly.FromDateTime(end.UtcDateTime)) ?? 0;

    private static string GetPlantColor(string plant) =>
        OptimizerPageDataBuilder.GetPlantColor(plant);

    private void SelectChartPlant(string? plant)
    {
        _selectedChartPlant = plant;
    }

    private string GetGridRowClass(PlantRecoveryGridRow row, int rowIndex)
    {
        if (string.Equals(row.Plant, _selectedChartPlant, StringComparison.OrdinalIgnoreCase))
        {
            return "selected-plant-row";
        }

        return !string.IsNullOrWhiteSpace(_plantFilter)
            && row.Plant.Contains(_plantFilter, StringComparison.OrdinalIgnoreCase)
                ? "searched-plant-row"
                : string.Empty;
    }

    private string GetGridDayHeader(int day) => FormatDayLabel(day);

    private string GetGridRecovery(PlantRecoveryGridRow row, int day) =>
        row.Recoveries.TryGetValue(day, out double recovery)
            ? recovery.ToString("0.00", CultureInfo.InvariantCulture)
            : "—";

    private string GetDayBoundaryClass(int day)
    {
        int[] days = RecoveryDays;
        return day == days.FirstOrDefault() ? "first-day-column"
            : day == days.LastOrDefault() ? "last-day-column"
            : string.Empty;
    }

    private void SelectGridPlant(string plant)
    {
        _selectedChartPlant = plant;
    }

    protected override void OnParametersSet()
    {
        if (string.Equals(_loadedModelId, ModelId, StringComparison.Ordinal))
        {
            return;
        }

        if (_versions.Length > 0)
        {
            CurrentScenario.Parameters = BuildOptimizeRequest();
        }

        _requestCancellation?.Cancel();
        _loadedModelId = ModelId;
        _versions = ScenarioStateStore.GetVersions(ModelId);
        _selectedVersionIndex = 0;
        ApplyScenarioParameters();
    }

    private Task VersionChanged(int index)
    {
        if (_isLoading || index < 0 || index >= _versions.Length || index == _selectedVersionIndex)
        {
            return Task.CompletedTask;
        }

        CurrentScenario.Parameters = BuildOptimizeRequest();
        _selectedVersionIndex = index;
        ApplyScenarioParameters();
        _errorMessage = null;
        _apiDetail = null;
        _serverValidationErrors = new Dictionary<string, string[]>();
        _clientValidationErrors = [];
        _selectedChartPlant = null;
        return Task.CompletedTask;
    }

    private void ApplyScenarioParameters()
    {
        OptimizeRequestDto parameters = CurrentScenario.Parameters;
        _planningStartDate = parameters.PlanningStartDate is DateOnly startDate
            ? new DateTimeOffset(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, TimeSpan.Zero)
            : null;
        _planningEndDate = parameters.PlanningEndDate is DateOnly endDate
            ? new DateTimeOffset(endDate.Year, endDate.Month, endDate.Day, 0, 0, 0, TimeSpan.Zero)
            : null;
        _ethanePriceShiftPct = parameters.EthanePriceShiftPct;
        _gasPriceShiftPct = parameters.GasPriceShiftPct;
        _tfFee = parameters.TfFee;
        _ethaneHeatingValue = parameters.EthaneHeatingValue;
        _takeawayBblPerDay = parameters.TakeawayBblPerDay;
        _kFix = parameters.KFix;
        _kVar = parameters.KVar;
        _maxChangesPerTrain = parameters.MaxChangesPerTrain;
        _minHoldDays = parameters.MinHoldDays;
        _timeLimitSec = parameters.TimeLimitSec;
        _mipGap = parameters.MipGap;
        _result = CurrentScenario.Result;
        _completedRequest = CurrentScenario.CompletedRequest;
        RefreshChartSeries();
    }

    private bool IsTrainOverrideEnabled(string trainId) =>
        CurrentScenario.EnabledTrainOverrides.Contains(trainId);

    private void SetTrainOverrideEnabled(string trainId, bool enabled)
    {
        if (enabled)
        {
            CurrentScenario.EnabledTrainOverrides.Add(trainId);
            CurrentScenario.TrainOverrides.TryAdd(trainId, new TrainSettingsDto());
        }
        else
        {
            CurrentScenario.EnabledTrainOverrides.Remove(trainId);
        }
    }

    private async Task CalculateAsync()
    {
        if (_isLoading || !IsPlanningPeriodValid)
        {
            return;
        }

        _errorMessage = null;
        _apiDetail = null;
        _serverValidationErrors = new Dictionary<string, string[]>();
        _clientValidationErrors = [];

        if (!TryCreateRequest(out OptimizeRequestDto request))
        {
            _errorMessage = "Please correct optimizer input values.";
            return;
        }

        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        OptimizerScenarioState scenario = CurrentScenario;
        CancellationTokenSource requestCancellation = new();
        _requestCancellation = requestCancellation;
        _isLoading = true;
        _result = null;
        _completedRequest = null;
        _selectedChartPlant = null;
        _chartSeries = [];
        Stopwatch elapsed = Stopwatch.StartNew();

        try
        {
            await InvokeAsync(StateHasChanged);
            OptimizeResponseDto response = await OptimizerApiClient.OptimizeAsync(
                request,
                requestCancellation.Token);

            elapsed.Stop();
            _result = response;
            _completedRequest = request;
            scenario.Parameters = request;
            scenario.Result = response;
            scenario.CompletedRequest = request;
            scenario.CompletedAt = DateTimeOffset.UtcNow;
            scenario.Elapsed = elapsed.Elapsed;
            foreach (string trainId in scenario.KnownTrainIds)
            {
                scenario.TrainOverrides.TryAdd(trainId, new TrainSettingsDto());
            }

            RefreshChartSeries();
            Snackbar.Add("Optimized recommendations calculated successfully.", Severity.Success);
        }
        catch (OptimizerApiException exception)
        {
            _errorMessage = exception.UserMessage;
            _apiDetail = exception.ApiDetail;
            _serverValidationErrors = exception.ValidationErrors;
            Snackbar.Add(exception.UserMessage, Severity.Error);
        }
        catch (HttpRequestException exception)
        {
            Logger.LogError(exception, "Unable to connect to the Optimizer API.");
            _errorMessage = "Unable to connect to the Optimizer API.";
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        catch (JsonException exception)
        {
            Logger.LogError(exception, "The Optimizer API returned an invalid success response.");
            _errorMessage = "The optimizer returned an unexpected response. Please try again later.";
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        catch (OperationCanceledException exception)
            when (!requestCancellation.IsCancellationRequested)
        {
            Logger.LogWarning(exception, "The Optimizer API request timed out.");
            _errorMessage = "The optimizer request timed out. Please try again.";
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _isLoading = false;
            if (_result is null && _versions.Length > _selectedVersionIndex
                && ReferenceEquals(CurrentScenario, scenario))
            {
                _result = scenario.Result;
                _completedRequest = scenario.CompletedRequest;
                RefreshChartSeries();
            }

            if (ReferenceEquals(_requestCancellation, requestCancellation))
            {
                _requestCancellation = null;
            }

            requestCancellation.Dispose();
        }
    }

    private bool TryCreateRequest(out OptimizeRequestDto request)
    {
        List<string> errors = [];
        DateOnly? start = _planningStartDate is DateTimeOffset startDate
            ? DateOnly.FromDateTime(startDate.UtcDateTime)
            : null;
        DateOnly? end = _planningEndDate is DateTimeOffset endDate
            ? DateOnly.FromDateTime(endDate.UtcDateTime)
            : null;

        if (start is null)
        {
            errors.Add("Planning start date is required.");
        }

        if (end is null)
        {
            errors.Add("Planning end date is required.");
        }

        int horizonDays = 0;
        if (start is DateOnly planningStart && end is DateOnly planningEnd)
        {
            horizonDays = planningEnd.DayNumber - planningStart.DayNumber + 1;
            if (horizonDays < 1)
            {
                errors.Add("Planning end date must be on or after the start date.");
            }
            else if (horizonDays > 90)
            {
                errors.Add("The inclusive planning period cannot exceed 90 days.");
            }
        }

        if (_timeLimitSec is < 1 or > 220)
        {
            errors.Add("Solver time limit must be between 1 and 220 seconds.");
        }

        if (_mipGap is < 0 or > 1)
        {
            errors.Add("MIP gap must be between 0 and 1.");
        }

        if (_maxChangesPerTrain < 0 || _minHoldDays < 0)
        {
            errors.Add("Maximum changes per train and minimum hold days cannot be negative.");
        }

        if (_tfFee < 0 || _kFix < 0 || _kVar < 0 || _takeawayBblPerDay < 0)
        {
            errors.Add("Fees, change costs, and takeaway cannot be negative.");
        }

        if (_ethaneHeatingValue <= 0)
        {
            errors.Add("Ethane heating value must be greater than zero.");
        }

        if (!double.IsFinite(_ethanePriceShiftPct)
            || !double.IsFinite(_gasPriceShiftPct)
            || !double.IsFinite(_tfFee)
            || !double.IsFinite(_ethaneHeatingValue)
            || !double.IsFinite(_kFix)
            || !double.IsFinite(_kVar)
            || !double.IsFinite(_mipGap)
            || (_takeawayBblPerDay is double takeaway && !double.IsFinite(takeaway)))
        {
            errors.Add("Optimizer numeric inputs must be finite numbers.");
        }

        foreach (string trainId in CurrentScenario.EnabledTrainOverrides)
        {
            if (!CurrentScenario.KnownTrainIds.Contains(trainId, StringComparer.OrdinalIgnoreCase)
                || !CurrentScenario.TrainOverrides.TryGetValue(trainId, out TrainSettingsDto? settings))
            {
                errors.Add($"Train override '{trainId}' is not a known train from the Optimizer API.");
                continue;
            }

            if (!IsValidRecovery(settings.RMin)
                || !IsValidRecovery(settings.RMax)
                || !IsValidRecovery(settings.R0)
                || (settings.LockedAt is double lockedAt && !IsValidRecovery(lockedAt))
                || settings.RMin > settings.RMax
                || settings.COp < 0
                || !double.IsFinite(settings.COp))
            {
                errors.Add($"Train override '{trainId}' must use recovery fractions between 0 and 1, with r_min not above r_max and a nonnegative operating cost.");
            }
        }

        _clientValidationErrors = errors;
        request = BuildOptimizeRequest();
        request.PlanningStartDate = start;
        request.PlanningEndDate = end;
        request.HorizonDays = horizonDays;
        return errors.Count == 0;
    }

    private OptimizeRequestDto BuildOptimizeRequest()
    {
        DateOnly? start = _planningStartDate is DateTimeOffset startDate
            ? DateOnly.FromDateTime(startDate.UtcDateTime)
            : null;
        DateOnly? end = _planningEndDate is DateTimeOffset endDate
            ? DateOnly.FromDateTime(endDate.UtcDateTime)
            : null;
        int horizonDays = start is DateOnly planningStart && end is DateOnly planningEnd
            ? planningEnd.DayNumber - planningStart.DayNumber + 1
            : 0;

        Dictionary<string, TrainSettingsDto> trains = CurrentScenario.KnownTrainIds
            .Where(CurrentScenario.EnabledTrainOverrides.Contains)
            .Where(trainId => CurrentScenario.TrainOverrides.ContainsKey(trainId))
            .ToDictionary(
                trainId => trainId,
                trainId => CopyTrainSettings(CurrentScenario.TrainOverrides[trainId]),
                StringComparer.Ordinal);

        return new OptimizeRequestDto
        {
            PlanningStartDate = start,
            PlanningEndDate = end,
            HorizonDays = horizonDays,
            EthanePriceShiftPct = _ethanePriceShiftPct,
            GasPriceShiftPct = _gasPriceShiftPct,
            TfFee = _tfFee,
            EthaneHeatingValue = _ethaneHeatingValue,
            TakeawayBblPerDay = _takeawayBblPerDay,
            KFix = _kFix,
            KVar = _kVar,
            MaxChangesPerTrain = _maxChangesPerTrain,
            MinHoldDays = _minHoldDays,
            TimeLimitSec = _timeLimitSec,
            MipGap = _mipGap,
            Trains = trains
        };
    }

    private static bool IsValidRecovery(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 1;

    private static TrainSettingsDto CopyTrainSettings(TrainSettingsDto settings) => new()
    {
        RMin = settings.RMin,
        RMax = settings.RMax,
        R0 = settings.R0,
        LockedAt = settings.LockedAt,
        COp = settings.COp
    };

    private static Color GetStatusColor(string status) =>
        status.Equals("optimal", StringComparison.OrdinalIgnoreCase)
        || status.Equals("feasible", StringComparison.OrdinalIgnoreCase)
            ? Color.Success
            : Color.Warning;

    private static string FormatRecovery(double recovery) =>
        OptimizerPageDataBuilder.FormatRecovery(recovery);

    private static string FormatMargin(double margin) =>
        margin.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

    public void Dispose()
    {
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;
    }
}

