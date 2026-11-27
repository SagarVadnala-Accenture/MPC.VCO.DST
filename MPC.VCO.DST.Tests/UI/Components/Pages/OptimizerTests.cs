using System.Reflection;
using System.Net;
using System.Collections;
using System.Text.Json;
using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using MPC.VCO.DST.WebSite.Components.Pages;
using MPC.VCO.DST.WebSite.Services.Optimizer;
using MPC.VCO.DST.WebSite.Services.Optimizer.Models;

namespace MPC.VCO.DST.Tests.UI.Components.Pages;

[TestClass]
public sealed class OptimizerTests
{
    [TestMethod]
    public void TryCreateRequest_MissingDates_ReturnsFalseWithValidationErrors()
    {
        Optimizer component = CreateWithSingleScenario();

        SetPrivateField(component, "_planningStartDate", null);
        SetPrivateField(component, "_planningEndDate", null);

        bool success = InvokeTryCreateRequest(component, out OptimizeRequestDto _);

        Assert.IsFalse(success);
        IReadOnlyList<string> errors = GetPrivateField<IReadOnlyList<string>>(component, "_clientValidationErrors");
        CollectionAssert.Contains(errors.ToList(), "Planning start date is required.");
        CollectionAssert.Contains(errors.ToList(), "Planning end date is required.");
    }

    [TestMethod]
    public void TryCreateRequest_InvalidScalarInputs_ReturnsFalseWithExpectedErrors()
    {
        Optimizer component = CreateWithSingleScenario();

        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_timeLimitSec", 0);
        SetPrivateField(component, "_mipGap", 1.5d);
        SetPrivateField(component, "_maxChangesPerTrain", -1);
        SetPrivateField(component, "_minHoldDays", -1);
        SetPrivateField(component, "_tfFee", -1d);
        SetPrivateField(component, "_kFix", -1d);
        SetPrivateField(component, "_kVar", -1d);
        SetPrivateField(component, "_takeawayBblPerDay", -1d);
        SetPrivateField(component, "_ethaneHeatingValue", 0d);

        bool success = InvokeTryCreateRequest(component, out OptimizeRequestDto _);

        Assert.IsFalse(success);
        IReadOnlyList<string> errors = GetPrivateField<IReadOnlyList<string>>(component, "_clientValidationErrors");
        CollectionAssert.Contains(errors.ToList(), "Planning end date must be on or after the start date.");
        CollectionAssert.Contains(errors.ToList(), "Solver time limit must be between 1 and 220 seconds.");
        CollectionAssert.Contains(errors.ToList(), "MIP gap must be between 0 and 1.");
        CollectionAssert.Contains(errors.ToList(), "Maximum changes per train and minimum hold days cannot be negative.");
        CollectionAssert.Contains(errors.ToList(), "Fees, change costs, and takeaway cannot be negative.");
        CollectionAssert.Contains(errors.ToList(), "Ethane heating value must be greater than zero.");
    }

    [TestMethod]
    public void TryCreateRequest_UnknownTrainOverride_ReturnsFalseWithTrainError()
    {
        Optimizer component = CreateWithSingleScenario();
        OptimizerScenarioState scenario = GetScenario(component);

        scenario.EnabledTrainOverrides.Add("TRAIN-X");

        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero));

        bool success = InvokeTryCreateRequest(component, out OptimizeRequestDto _);

        Assert.IsFalse(success);
        IReadOnlyList<string> errors = GetPrivateField<IReadOnlyList<string>>(component, "_clientValidationErrors");
        Assert.IsTrue(errors.Any(error => error.Contains("TRAIN-X", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TryCreateRequest_ValidInputs_ReturnsTrueAndSetsHorizonDays()
    {
        Optimizer component = CreateWithSingleScenario();

        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_timeLimitSec", 100);
        SetPrivateField(component, "_mipGap", 0.1d);
        SetPrivateField(component, "_maxChangesPerTrain", 1);
        SetPrivateField(component, "_minHoldDays", 1);
        SetPrivateField(component, "_tfFee", 1d);
        SetPrivateField(component, "_kFix", 1d);
        SetPrivateField(component, "_kVar", 1d);
        SetPrivateField(component, "_takeawayBblPerDay", 1d);
        SetPrivateField(component, "_ethaneHeatingValue", 1d);
        SetPrivateField(component, "_ethanePriceShiftPct", 0d);
        SetPrivateField(component, "_gasPriceShiftPct", 0d);

        bool success = InvokeTryCreateRequest(component, out OptimizeRequestDto request);

        Assert.IsTrue(success);
        Assert.AreEqual(30, request.HorizonDays);
        Assert.AreEqual(new DateOnly(2026, 7, 1), request.PlanningStartDate);
        Assert.AreEqual(new DateOnly(2026, 7, 30), request.PlanningEndDate);
    }

    [TestMethod]
    public void BuildOptimizeRequest_EnabledTrainOverrides_AreCopiedIntoRequest()
    {
        Optimizer component = CreateWithSingleScenario();
        OptimizerScenarioState scenario = GetScenario(component);

        scenario.Result = new OptimizeResponseDto
        {
            TrainSchedule =
            [
                new TrainDayDto { Train = "T1", Plant = "P1", Day = 1, Recovery = 0.4, Changed = false },
                new TrainDayDto { Train = "T2", Plant = "P2", Day = 1, Recovery = 0.5, Changed = true }
            ]
        };

        scenario.EnabledTrainOverrides.Add("T1");
        scenario.TrainOverrides["T1"] = new TrainSettingsDto
        {
            RMin = 0.1,
            RMax = 0.9,
            R0 = 0.3,
            LockedAt = 0.5,
            COp = 2.5
        };

        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero));

        OptimizeRequestDto request = (OptimizeRequestDto)InvokePrivate(component, "BuildOptimizeRequest")!;

        Assert.AreEqual(1, request.Trains.Count);
        Assert.IsTrue(request.Trains.ContainsKey("T1"));
        Assert.IsFalse(request.Trains.ContainsKey("T2"));
        Assert.AreEqual(0.1, request.Trains["T1"].RMin);
        Assert.AreEqual(0.9, request.Trains["T1"].RMax);
        Assert.IsFalse(ReferenceEquals(request.Trains["T1"], scenario.TrainOverrides["T1"]));
    }

    [TestMethod]
    public void SetTrainOverrideEnabled_AddThenRemove_TracksEnabledSet()
    {
        Optimizer component = CreateWithSingleScenario();

        InvokePrivate(component, "SetTrainOverrideEnabled", "TR-1", true);
        bool enabled = (bool)InvokePrivate(component, "IsTrainOverrideEnabled", "TR-1")!;

        InvokePrivate(component, "SetTrainOverrideEnabled", "TR-1", false);
        bool disabled = (bool)InvokePrivate(component, "IsTrainOverrideEnabled", "TR-1")!;

        Assert.IsTrue(enabled);
        Assert.IsFalse(disabled);
    }

    [TestMethod]
    public async Task VersionChanged_SameIndexOrInvalidIndex_NoStateMutation()
    {
        Optimizer component = new();
        OptimizerScenarioState[] versions =
        [
            new(),
            new()
        ];

        SetPrivateField(component, "_versions", versions);
        SetPrivateField(component, "_selectedVersionIndex", 0);

        await InvokePrivateTask(component, "VersionChanged", -1);
        await InvokePrivateTask(component, "VersionChanged", 0);
        await InvokePrivateTask(component, "VersionChanged", 99);

        Assert.AreEqual(0, GetPrivateField<int>(component, "_selectedVersionIndex"));
    }

    [TestMethod]
    public void GetGridRecovery_And_DayBoundaryBranches_ReturnExpectedValues()
    {
        Optimizer component = CreateWithSingleScenario();
        var row = new PlantRecoveryGridRow(
            "P1",
            [1, 2],
            new Dictionary<int, double> { [1] = 0.42 },
            0.42);

        SetPrivateField(component, "_result", new OptimizeResponseDto
        {
            PlantRecovery =
            [
                new PlantDayDto { Plant = "P1", Day = 1, Recovery = 0.42 },
                new PlantDayDto { Plant = "P1", Day = 3, Recovery = 0.52 }
            ]
        });

        string present = (string)InvokePrivate(component, "GetGridRecovery", row, 1)!;
        string missing = (string)InvokePrivate(component, "GetGridRecovery", row, 2)!;
        string firstBoundary = (string)InvokePrivate(component, "GetDayBoundaryClass", 1)!;
        string lastBoundary = (string)InvokePrivate(component, "GetDayBoundaryClass", 3)!;
        string middleBoundary = (string)InvokePrivate(component, "GetDayBoundaryClass", 2)!;

        Assert.AreEqual("0.42", present);
        Assert.AreEqual("—", missing);
        Assert.AreEqual("first-day-column", firstBoundary);
        Assert.AreEqual("last-day-column", lastBoundary);
        Assert.AreEqual(string.Empty, middleBoundary);
    }

    [TestMethod]
    public void GetGridRowClass_SelectedAndSearchBranches_ReturnExpectedCssClass()
    {
        Optimizer component = CreateWithSingleScenario();
        var row = new PlantRecoveryGridRow("Mont Belvieu", [], new Dictionary<int, double>(), null);

        SetPrivateField(component, "_selectedChartPlant", "Mont Belvieu");
        string selectedClass = (string)InvokePrivate(component, "GetGridRowClass", row, 0)!;

        SetPrivateField(component, "_selectedChartPlant", null);
        SetPrivateField(component, "_plantFilter", "belvieu");
        string searchedClass = (string)InvokePrivate(component, "GetGridRowClass", row, 0)!;

        SetPrivateField(component, "_plantFilter", "other");
        string defaultClass = (string)InvokePrivate(component, "GetGridRowClass", row, 0)!;

        Assert.AreEqual("selected-plant-row", selectedClass);
        Assert.AreEqual("searched-plant-row", searchedClass);
        Assert.AreEqual(string.Empty, defaultClass);
    }

    [TestMethod]
    public async Task CalculateAsync_ValidResponse_UpdatesResultScenarioAndChartState()
    {
        await using BunitContext context = new();
        context.Services.AddMudServices();

        FakeOptimizerApiClient apiClient = new()
        {
            OptimizeHandler = (_, _) => Task.FromResult(new OptimizeResponseDto
            {
                ScenarioId = "scenario-1",
                Status = "optimal",
                PortfolioMarginUsd = 1234,
                TrainSchedule =
                [
                    new TrainDayDto { Plant = "P2", Train = "T2", Day = 2, Recovery = 0.51, Changed = false },
                    new TrainDayDto { Plant = "P1", Train = "T1", Day = 1, Recovery = 0.45, Changed = true }
                ],
                PlantRecovery =
                [
                    new PlantDayDto { Plant = "P2", Day = 2, Recovery = 0.51 },
                    new PlantDayDto { Plant = "P1", Day = 1, Recovery = 0.45 }
                ]
            })
        };

        IRenderedComponent<Optimizer> cut = RenderOptimizer(context, apiClient);

        cut.Find("button.calculate-button").Click();
        cut.WaitForAssertion(() => Assert.AreEqual(1, apiClient.OptimizeCalls));

        OptimizeResponseDto? result = GetPrivateField<OptimizeResponseDto?>(cut.Instance, "_result");
        OptimizeRequestDto? completedRequest = GetPrivateField<OptimizeRequestDto?>(cut.Instance, "_completedRequest");
        IList chartSeries = GetPrivateField<IList>(cut.Instance, "_chartSeries");
        OptimizerScenarioState scenario = GetRenderedScenario(cut.Instance);

        Assert.IsNotNull(result);
        Assert.IsNotNull(completedRequest);
        Assert.AreEqual("scenario-1", result.ScenarioId);
        Assert.IsNotNull(scenario.Result);
        Assert.IsNotNull(scenario.CompletedRequest);
        Assert.IsTrue(chartSeries.Count > 0);
    }

    [TestMethod]
    public async Task CalculateAsync_OptimizerApiException_SetsErrorAndValidationDetails()
    {
        await using BunitContext context = new();
        context.Services.AddMudServices();

        FakeOptimizerApiClient apiClient = new()
        {
            OptimizeHandler = (_, _) => Task.FromException<OptimizeResponseDto>(
                new OptimizerApiException(
                    HttpStatusCode.BadRequest,
                    "Please correct optimizer input values.",
                    "invalid payload",
                    new Dictionary<string, string[]> { ["horizon_days"] = ["Must be >= 1"] }))
        };

        IRenderedComponent<Optimizer> cut = RenderOptimizer(context, apiClient);

        cut.Find("button.calculate-button").Click();
        cut.WaitForAssertion(() => Assert.AreEqual(1, apiClient.OptimizeCalls));

        string? error = GetPrivateField<string?>(cut.Instance, "_errorMessage");
        string? detail = GetPrivateField<string?>(cut.Instance, "_apiDetail");
        IReadOnlyDictionary<string, string[]> validationErrors =
            GetPrivateField<IReadOnlyDictionary<string, string[]>>(cut.Instance, "_serverValidationErrors");

        Assert.AreEqual("Please correct optimizer input values.", error);
        Assert.AreEqual("invalid payload", detail);
        Assert.IsTrue(validationErrors.ContainsKey("horizon_days"));
    }

    [TestMethod]
    public async Task CalculateAsync_CanceledRequest_DoesNotSetError()
    {
        await using BunitContext context = new();
        context.Services.AddMudServices();

        FakeOptimizerApiClient apiClient = new()
        {
            OptimizeHandler = async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new OptimizeResponseDto();
            }
        };

        IRenderedComponent<Optimizer> cut = RenderOptimizer(context, apiClient);

        Task calculateTask = InvokePrivateTask(cut.Instance, "CalculateAsync");
        await Task.Delay(50);
        cut.Instance.Dispose();
        await calculateTask;

        Assert.IsNull(GetPrivateField<string?>(cut.Instance, "_errorMessage"));
        Assert.IsFalse(GetPrivateField<bool>(cut.Instance, "_isLoading"));
    }

    [TestMethod]
    public async Task CalculateAsync_WhenAlreadyLoading_DoesNotSubmitAgain()
    {
        Optimizer component = CreateWithSingleScenario();
        SetPrivateField(component, "_isLoading", true);

        await InvokePrivateTask(component, "CalculateAsync");

        Assert.IsTrue(GetPrivateField<bool>(component, "_isLoading"));
    }

    [TestMethod]
    public async Task CalculateAsync_InvalidRequest_SetsErrorAndSkipsApiCall()
    {
        Optimizer component = CreateWithSingleScenario();
        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_timeLimitSec", 0);

        await InvokePrivateTask(component, "CalculateAsync");

        Assert.AreEqual("Please correct optimizer input values.", GetPrivateField<string?>(component, "_errorMessage"));
        Assert.IsTrue(GetPrivateField<IReadOnlyList<string>>(component, "_clientValidationErrors").Count > 0);
    }

    [TestMethod]
    public void TryCreateRequest_HorizonExceedsNinetyDays_ReturnsExpectedError()
    {
        Optimizer component = CreateWithSingleScenario();

        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero));

        bool success = InvokeTryCreateRequest(component, out OptimizeRequestDto _);

        Assert.IsFalse(success);
        IReadOnlyList<string> errors = GetPrivateField<IReadOnlyList<string>>(component, "_clientValidationErrors");
        CollectionAssert.Contains(errors.ToList(), "The inclusive planning period cannot exceed 90 days.");
    }

    [TestMethod]
    public void TryCreateRequest_NonFiniteNumericValues_ReturnsExpectedError()
    {
        Optimizer component = CreateWithSingleScenario();

        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_ethanePriceShiftPct", double.NaN);

        bool success = InvokeTryCreateRequest(component, out OptimizeRequestDto _);

        Assert.IsFalse(success);
        IReadOnlyList<string> errors = GetPrivateField<IReadOnlyList<string>>(component, "_clientValidationErrors");
        CollectionAssert.Contains(errors.ToList(), "Optimizer numeric inputs must be finite numbers.");
    }

    [TestMethod]
    public void TryCreateRequest_InvalidTrainOverrideSettings_ReturnsExpectedError()
    {
        Optimizer component = CreateWithSingleScenario();
        OptimizerScenarioState scenario = GetScenario(component);

        scenario.Result = new OptimizeResponseDto
        {
            TrainSchedule =
            [
                new TrainDayDto { Train = "TR-1", Plant = "P1", Day = 1, Recovery = 0.5, Changed = false }
            ]
        };
        scenario.EnabledTrainOverrides.Add("TR-1");
        scenario.TrainOverrides["TR-1"] = new TrainSettingsDto
        {
            RMin = 0.9,
            RMax = 0.2,
            R0 = 0.4,
            LockedAt = 0.5,
            COp = -1
        };

        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero));

        bool success = InvokeTryCreateRequest(component, out OptimizeRequestDto _);

        Assert.IsFalse(success);
        IReadOnlyList<string> errors = GetPrivateField<IReadOnlyList<string>>(component, "_clientValidationErrors");
        Assert.IsTrue(errors.Any(error => error.Contains("TR-1", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void FilteredTrainSchedule_AppliesPlantTrainChangedAndDayFilters()
    {
        Optimizer component = CreateWithSingleScenario();
        SetPrivateField(component, "_result", new OptimizeResponseDto
        {
            TrainSchedule =
            [
                new TrainDayDto { Plant = "P1", Train = "TR-1", Day = 2, Changed = true },
                new TrainDayDto { Plant = "P1", Train = "TR-2", Day = 2, Changed = false },
                new TrainDayDto { Plant = "P2", Train = "TR-1", Day = 2, Changed = true }
            ]
        });

        SetPrivateField(component, "_schedulePlantFilter", "P1");
        SetPrivateField(component, "_scheduleTrainFilter", "TR-1");
        SetPrivateField(component, "_scheduleChangedFilter", "Changed");
        SetPrivateField(component, "_scheduleDayFilter", 2);

        IEnumerable<TrainDayDto> rows = GetPrivateProperty<IEnumerable<TrainDayDto>>(component, "FilteredTrainSchedule");
        TrainDayDto[] filtered = rows.ToArray();

        Assert.AreEqual(1, filtered.Length);
        Assert.AreEqual("P1", filtered[0].Plant);
        Assert.AreEqual("TR-1", filtered[0].Train);
    }

    [TestMethod]
    public void ChartOptions_AndStaticHelpers_ReturnExpectedFormattingAndColors()
    {
        Optimizer component = CreateWithSingleScenario();
        var options = GetPrivateProperty<object>(component, "ChartOptions");
        Func<double, string> yAxisFormat = (Func<double, string>)options.GetType()
            .GetProperty("YAxisToStringFunc")!
            .GetValue(options)!;

        string formatted = yAxisFormat(0.25);
        MethodInfo? calculateInclusiveHorizonMethod = typeof(Optimizer)
            .GetMethod("CalculateInclusiveHorizon", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(calculateInclusiveHorizonMethod);
        int inclusiveHorizon = (int)calculateInclusiveHorizonMethod!
            .Invoke(null, [new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero)])!;
        string fallbackLabel = (string)InvokePrivate(component, "FormatDayLabel", 3)!;
        MethodInfo? getStatusColorMethod = typeof(Optimizer)
            .GetMethod("GetStatusColor", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(getStatusColorMethod);
        object statusColor = getStatusColorMethod!
            .Invoke(null, ["optimal"])!;

        StringAssert.Contains(formatted, "25");
        Assert.AreEqual(10, inclusiveHorizon);
        Assert.AreEqual("Day 3", fallbackLabel);
        Assert.AreEqual("Success", statusColor.ToString());
    }

    [TestMethod]
    public void SelectChartPlant_And_SelectGridPlant_UpdateSelectedPlant()
    {
        Optimizer component = CreateWithSingleScenario();

        InvokePrivate(component, "SelectChartPlant", "P1");
        Assert.AreEqual("P1", GetPrivateField<string?>(component, "_selectedChartPlant"));

        InvokePrivate(component, "SelectGridPlant", "P2");
        Assert.AreEqual("P2", GetPrivateField<string?>(component, "_selectedChartPlant"));
    }

    [TestMethod]
    public async Task VersionChanged_ValidIndex_ResetsUiErrorStateAndSwitchesScenario()
    {
        Optimizer component = new();
        OptimizerScenarioState[] versions = [new(), new()];
        SetPrivateField(component, "_versions", versions);
        SetPrivateField(component, "_selectedVersionIndex", 0);
        SetPrivateField(component, "_planningStartDate", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_planningEndDate", new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero));
        SetPrivateField(component, "_errorMessage", "old-error");
        SetPrivateField(component, "_apiDetail", "old-detail");
        SetPrivateField(component, "_selectedChartPlant", "Plant-A");

        await InvokePrivateTask(component, "VersionChanged", 1);

        Assert.AreEqual(1, GetPrivateField<int>(component, "_selectedVersionIndex"));
        Assert.IsNull(GetPrivateField<string?>(component, "_errorMessage"));
        Assert.IsNull(GetPrivateField<string?>(component, "_apiDetail"));
        Assert.IsNull(GetPrivateField<string?>(component, "_selectedChartPlant"));
        Assert.IsNotNull(versions[0].Parameters);
    }

    [TestMethod]
    public async Task CalculateAsync_HttpRequestException_SetsConnectivityError()
    {
        await using BunitContext context = new();
        context.Services.AddMudServices();

        FakeOptimizerApiClient apiClient = new()
        {
            OptimizeHandler = (_, _) => Task.FromException<OptimizeResponseDto>(new HttpRequestException("offline"))
        };

        IRenderedComponent<Optimizer> cut = RenderOptimizer(context, apiClient);

        cut.Find("button.calculate-button").Click();
        cut.WaitForAssertion(() => Assert.AreEqual("Unable to connect to the Optimizer API.",
            GetPrivateField<string?>(cut.Instance, "_errorMessage")));
    }

    [TestMethod]
    public async Task CalculateAsync_JsonException_SetsUnexpectedResponseError()
    {
        await using BunitContext context = new();
        context.Services.AddMudServices();

        FakeOptimizerApiClient apiClient = new()
        {
            OptimizeHandler = (_, _) => Task.FromException<OptimizeResponseDto>(new JsonException("bad json"))
        };

        IRenderedComponent<Optimizer> cut = RenderOptimizer(context, apiClient);

        cut.Find("button.calculate-button").Click();
        cut.WaitForAssertion(() => Assert.AreEqual(
            "The optimizer returned an unexpected response. Please try again later.",
            GetPrivateField<string?>(cut.Instance, "_errorMessage")));
    }

    [TestMethod]
    public async Task CalculateAsync_TimeoutCancellation_SetsTimeoutError_And_RestoresScenarioResult()
    {
        await using BunitContext context = new();
        context.Services.AddMudServices();

        FakeOptimizerApiClient apiClient = new()
        {
            OptimizeHandler = (_, _) => Task.FromException<OptimizeResponseDto>(new OperationCanceledException("timeout"))
        };

        IRenderedComponent<Optimizer> cut = RenderOptimizer(context, apiClient);
        OptimizerScenarioState scenario = GetRenderedScenario(cut.Instance);
        scenario.Result = new OptimizeResponseDto { ScenarioId = "existing" };

        cut.Find("button.calculate-button").Click();
        cut.WaitForAssertion(() => Assert.AreEqual(
            "The optimizer request timed out. Please try again.",
            GetPrivateField<string?>(cut.Instance, "_errorMessage")));

        OptimizeResponseDto? restoredResult = GetPrivateField<OptimizeResponseDto?>(cut.Instance, "_result");
        Assert.IsNotNull(restoredResult);
        Assert.AreEqual("existing", restoredResult.ScenarioId);
    }

    private static IRenderedComponent<Optimizer> RenderOptimizer(BunitContext context, FakeOptimizerApiClient apiClient)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IOptimizerApiClient>(apiClient);
        context.Services.AddSingleton(new OptimizerScenarioStateStore());
        context.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<Optimizer>>(NullLogger<Optimizer>.Instance);

        return context.Render<Optimizer>(parameters =>
            parameters.Add(component => component.ModelId, "M-001"));
    }

    private static OptimizerScenarioState GetRenderedScenario(Optimizer component)
    {
        OptimizerScenarioState[] versions = GetPrivateField<OptimizerScenarioState[]>(component, "_versions");
        int selectedVersion = GetPrivateField<int>(component, "_selectedVersionIndex");
        return versions[selectedVersion];
    }

    private static Optimizer CreateWithSingleScenario()
    {
        Optimizer component = new();
        OptimizerScenarioState[] versions = [new OptimizerScenarioState()];
        SetPrivateField(component, "_versions", versions);
        SetPrivateField(component, "_selectedVersionIndex", 0);
        return component;
    }

    private static OptimizerScenarioState GetScenario(Optimizer component)
    {
        OptimizerScenarioState[] versions = GetPrivateField<OptimizerScenarioState[]>(component, "_versions");
        return versions[0];
    }

    private static bool InvokeTryCreateRequest(Optimizer component, out OptimizeRequestDto request)
    {
        MethodInfo method = GetMethod(typeof(Optimizer), "TryCreateRequest");
        object?[] args = [null!];
        bool success = (bool)method.Invoke(component, args)!;
        request = (OptimizeRequestDto)args[0]!;
        return success;
    }

    private static object? InvokePrivate(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = GetMethod(instance.GetType(), methodName);
        return method.Invoke(instance, args);
    }

    private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = GetMethod(instance.GetType(), methodName);
        Task task = (Task)method.Invoke(instance, args)!;
        await task;
    }

    private static MethodInfo GetMethod(Type type, string methodName)
    {
        MethodInfo? method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
        return method!;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
        field!.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
        return (T)field!.GetValue(instance)!;
    }

    private static T GetPrivateProperty<T>(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(property, $"Property '{propertyName}' was not found.");
        return (T)property!.GetValue(instance)!;
    }

    private sealed class FakeOptimizerApiClient : IOptimizerApiClient
    {
        public int OptimizeCalls { get; private set; }

        public Func<OptimizeRequestDto, CancellationToken, Task<OptimizeResponseDto>> OptimizeHandler { get; set; } =
            (_, _) => Task.FromResult(new OptimizeResponseDto());

       
        public Task<OptimizeResponseDto> OptimizeAsync(
            OptimizeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            OptimizeCalls++;
            return OptimizeHandler(request, cancellationToken);
        }
    }
}

