using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPC.VCO.DST.WebSite.Components.Pages;
using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services;
using MudBlazor;

namespace MPC.VCO.DST.Tests.UI.Components.Pages;

[TestClass]
public sealed class ModelHistoryTests
{
    [TestMethod]
    public void OpenCreateModel_CurrentPeriod_SetsCreateOpenTrue()
    {
        ModelHistory component = new();
        DateOnly currentPeriod = GetStaticField<DateOnly>("CurrentPeriod");

        SetPrivateField(component, "_period", currentPeriod);
        SetPrivateField(component, "_createOpen", false);

        InvokePrivateVoid(component, "OpenCreateModel");

        Assert.IsTrue(GetPrivateField<bool>(component, "_createOpen"));
    }

    [TestMethod]
    public async Task CreateModelAsync_MissingPlanningCycleId_LeavesDrawerOpen()
    {
        ModelHistory component = new();
        DateOnly period = new(2026, 7, 1);
        Dictionary<DateOnly, PlanningCycle> cycles = GetPrivateField<Dictionary<DateOnly, PlanningCycle>>(component, "_cycles");

        cycles[period] = new PlanningCycle(string.Empty, period, 0, null, null, null);
        SetPrivateField(component, "_period", period);
        SetPrivateField(component, "_createOpen", true);
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);

        await InvokePrivateTask(component, "CreateModelAsync", new ModelUpsertRequest
        {
            Status = "Draft",
            ScopeType = "Refinery",
            ScopeName = "TX",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.1m,
            NaturalGasPriceUsdPerMmbtu = 2.2m
        });

        Assert.IsTrue(GetPrivateField<bool>(component, "_createOpen"));
    }

    [TestMethod]
    public async Task OnParametersSetAsync_PreviewStateError_SetsHasErrorAndSkipsModelLoad()
    {
        ModelHistory component = new();
        component.PreviewState = "error";
        component.PreviewCycle = "2026-07";

        SetInjectedProperty(component, "PlanningCyclesApiClient", CreatePlanningCyclesApiClient((request, _) =>
        {
            Dictionary<string, string> query = ParseQuery(request.RequestUri!);
            int year = int.Parse(query["year"]);
            int month = int.Parse(query["month"]);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new
                    {
                        planningCycleId = $"PC-{year}-{month:00}",
                        year,
                        month,
                        name = "Cycle",
                        cycleState = "Current",
                        bestModel = (object?)null
                    }
                })
            });
        }));
        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            })));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);

        await InvokePrivateTask(component, "OnParametersSetAsync");

        Assert.IsTrue(GetPrivateField<bool>(component, "_hasError"));
        Assert.AreEqual(0, GetPrivateField<IReadOnlyList<ModelRun>>(component, "_allModels").Count);
    }

    [TestMethod]
    public async Task Retry_ApiFailure_LeavesErrorStateTrue()
    {
        ModelHistory component = new();

        SetPrivateField(component, "_hasError", true);
        SetInjectedProperty(component, "PlanningCyclesApiClient", CreatePlanningCyclesApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);

        await InvokePrivateTask(component, "Retry");

        Assert.IsTrue(GetPrivateField<bool>(component, "_hasError"));
    }

    [TestMethod]
    public void IsSorted_And_GetStatusColor_ReturnExpectedValues()
    {
        ModelHistory component = new();

        InvokePrivateVoid(component, "SortChanged", new SortRequest("Id", false));

        bool isSorted = (bool)InvokePrivate(component, "IsSorted", "Id")!;
        MethodInfo? getStatusColor = typeof(ModelHistory).GetMethod("GetStatusColor", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(getStatusColor);
        object executableColor = getStatusColor!.Invoke(null, [ModelStatus.Executable])!;
        object reviewedColor = getStatusColor.Invoke(null, [ModelStatus.Reviewed])!;
        object draftColor = getStatusColor.Invoke(null, [ModelStatus.Draft])!;

        Assert.IsTrue(isSorted);
        Assert.AreEqual("Success", executableColor.ToString());
        Assert.AreEqual("Warning", reviewedColor.ToString());
        Assert.AreEqual("Default", draftColor.ToString());
    }

    [TestMethod]
    public void GetCycle_MissingPeriod_ReturnsFallbackCycle()
    {
        ModelHistory component = new();
        DateOnly period = new(2026, 7, 1);

        PlanningCycle cycle = (PlanningCycle)InvokePrivate(component, "GetCycle", period)!;

        Assert.AreEqual(string.Empty, cycle.PlanningCycleId);
        Assert.AreEqual(period, cycle.Period);
    }

    [TestMethod]
    public void Columns_Field_IsInitializedWithExpectedEntries()
    {
        FieldInfo? columnsField = typeof(ModelHistory).GetField("Columns", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(columnsField);

        Array columns = (Array)columnsField!.GetValue(null)!;
        ModelRun model = CreateModelRun("PC-2026-07", "M-001");

        Assert.AreEqual(8, columns.Length);

        foreach (object column in columns)
        {
            PropertyInfo? selectorProperty = column.GetType().GetProperty("Selector", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(selectorProperty);
            Func<ModelRun, IComparable> selector = (Func<ModelRun, IComparable>)selectorProperty!.GetValue(column)!;

            IComparable value = selector(model);
            Assert.IsNotNull(value);
        }
    }

    [TestMethod]
    public void GetNotesActionButtonId_ReturnsExpectedCompositeId()
    {
        ModelRun model = CreateModelRun("PC-2026-07", "M-001");
        MethodInfo? method = typeof(ModelHistory).GetMethod("GetNotesActionButtonId", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        string buttonId = (string)method!.Invoke(null, [model])!;

        Assert.AreEqual("notes-action-PC-2026-07-M-001", buttonId);
    }

    [TestMethod]
    public void CycleHasModels_And_VisibleRows_ReflectCurrentState()
    {
        ModelHistory component = new();
        IReadOnlyList<ModelRun> filtered =
        [
            CreateModelRun("PC-2026-07", "M-001"),
            CreateModelRun("PC-2026-07", "M-002"),
            CreateModelRun("PC-2026-07", "M-003")
        ];

        SetPrivateField(component, "_allModels", filtered);
        SetPrivateField(component, "_filtered", filtered);
        SetPrivateField(component, "_page", 1);

        PropertyInfo? cycleHasModelsProperty = typeof(ModelHistory).GetProperty("CycleHasModels", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(cycleHasModelsProperty);
        bool cycleHasModels = (bool)cycleHasModelsProperty!.GetValue(component)!;

        PropertyInfo? visibleRowsProperty = typeof(ModelHistory).GetProperty("VisibleRows", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(visibleRowsProperty);
        IEnumerable<ModelRun> visibleRows = (IEnumerable<ModelRun>)visibleRowsProperty!.GetValue(component)!;

        Assert.IsTrue(cycleHasModels);
        Assert.AreEqual(3, visibleRows.Count());
    }

    [TestMethod]
    public void Refresh_TabIndexOutOfRange_ResetsToAllModelsTab()
    {
        ModelHistory component = new();
        IReadOnlyList<ModelRun> models =
        [
            CreateModelRun("PC-2026-07", "M-001") with { ModelType = "Type-A" },
            CreateModelRun("PC-2026-07", "M-002") with { ModelType = "Type-B" }
        ];

        SetPrivateField(component, "_allModels", models);
        SetPrivateField(component, "_tabIndex", 99);

        InvokePrivateVoid(component, "Refresh");

        Assert.AreEqual(0, GetPrivateField<int>(component, "_tabIndex"));
    }

    [TestMethod]
    public void Dispose_CancelsAndClearsPendingNotesRequest()
    {
        ModelHistory component = new();
        CancellationTokenSource requestSource = new();
        SetPrivateField(component, "_notesRequestSource", requestSource);

        component.Dispose();

        Assert.IsTrue(requestSource.IsCancellationRequested);
        Assert.IsNull(GetPrivateField<CancellationTokenSource?>(component, "_notesRequestSource"));
    }

    [TestMethod]
    public async Task PostNoteAsync_Success_UpdatesNotesAndClearsDraft()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = "note-1",
                    modelId = "M-001",
                    noteText = "hello",
                    createdAtUtc = DateTimeOffset.UtcNow,
                    author = new { displayName = "User", userPrincipalName = "user@contoso.com" },
                    mentions = Array.Empty<object>()
                })
            })));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_selectedModel", selected);
        SetPrivateField(component, "_notes", (IReadOnlyList<ModelNote>)
        [
            new ModelNote("old", "M-001", "User", "user@contoso.com", "old note", DateTimeOffset.UtcNow.AddMinutes(-5), [])
        ]);
        SetPrivateField(component, "_noteDraft", "  hello  ");
        SetPrivateField(component, "_noteMentions", (IReadOnlyList<NoteMention>)[]);

        await InvokePrivateTask(component, "PostNoteAsync");

        IReadOnlyList<ModelNote> notes = GetPrivateField<IReadOnlyList<ModelNote>>(component, "_notes");
        Assert.AreEqual(2, notes.Count);
        Assert.AreEqual("note-1", notes[0].NoteId);
        Assert.AreEqual(string.Empty, GetPrivateField<string>(component, "_noteDraft"));
        Assert.AreEqual(0, GetPrivateField<IReadOnlyList<NoteMention>>(component, "_noteMentions").Count);
        Assert.IsFalse(GetPrivateField<bool>(component, "_postingNote"));
    }

    [TestMethod]
    public async Task PostNoteAsync_UnauthorizedHttpError_ResetsPostingState()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_selectedModel", selected);
        SetPrivateField(component, "_noteDraft", "hello");

        await InvokePrivateTask(component, "PostNoteAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_postingNote"));
    }

    [TestMethod]
    public async Task PostNoteAsync_EmptyPayloadFromApi_ResetsPostingState()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<object?>(null)
            })));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_selectedModel", selected);
        SetPrivateField(component, "_noteDraft", "hello");

        await InvokePrivateTask(component, "PostNoteAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_postingNote"));
    }

    [TestMethod]
    public async Task LoadNotesAsync_WhenNoLongerActiveRequest_DoesNotSetError()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
        {
            SetPrivateField(component, "_notesOpen", false);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_selectedModel", selected);

        await InvokePrivateTask(component, "LoadNotesAsync", selected);

        Assert.IsFalse(GetPrivateField<bool>(component, "_notesError"));
    }

    [TestMethod]
    public void OpenCreateModel_ReadOnlyPeriod_DoesNotOpenCreateDrawer()
    {
        ModelHistory component = new();
        DateOnly currentPeriod = GetStaticField<DateOnly>("CurrentPeriod");

        SetPrivateField(component, "_period", currentPeriod.AddMonths(-1));
        SetPrivateField(component, "_createOpen", false);

        InvokePrivateVoid(component, "OpenCreateModel");

        Assert.IsFalse(GetPrivateField<bool>(component, "_createOpen"));
    }

    [TestMethod]
    public void CreateOpenChanged_UpdatesCreateOpenState()
    {
        ModelHistory component = new();

        InvokePrivateVoid(component, "CreateOpenChanged", true);
        Assert.IsTrue(GetPrivateField<bool>(component, "_createOpen"));

        InvokePrivateVoid(component, "CreateOpenChanged", false);
        Assert.IsFalse(GetPrivateField<bool>(component, "_createOpen"));
    }

    [TestMethod]
    public async Task NoteDraftChanged_NullValue_SetsEmptyString()
    {
        ModelHistory component = new();

        await InvokePrivateTask(component, "NoteDraftChanged", (object?)null);

        Assert.AreEqual(string.Empty, GetPrivateField<string>(component, "_noteDraft"));
    }

    [TestMethod]
    public async Task NoteMentionsChanged_ValidMentions_AssignsCollection()
    {
        ModelHistory component = new();
        IReadOnlyList<NoteMention> mentions =
        [
            new NoteMention("u1", "User One", "u1@company.com", 0, 5)
        ];

        await InvokePrivateTask(component, "NoteMentionsChanged", mentions);

        IReadOnlyList<NoteMention> assigned = GetPrivateField<IReadOnlyList<NoteMention>>(component, "_noteMentions");
        Assert.AreEqual(1, assigned.Count);
        Assert.AreEqual("u1", assigned[0].UserId);
    }

    [TestMethod]
    public async Task NotesOpenChanged_ClosingDrawer_ResetsNotesStateAndRestoresFocus()
    {
        ModelHistory component = new();
        TestJsRuntime jsRuntime = new();

        SetInjectedProperty(component, "JSRuntime", jsRuntime);
        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_loadingNotes", true);
        SetPrivateField(component, "_postingNote", true);
        SetPrivateField(component, "_notesError", true);
        SetPrivateField(component, "_noteDraft", "hello");
        SetPrivateField(component, "_noteMentions", (IReadOnlyList<NoteMention>)[new NoteMention("u1", "User", "u@company.com", 0, 4)]);
        SetPrivateField(component, "_notes", (IReadOnlyList<ModelNote>)[new ModelNote("n1", "m1", "User", "user", "text", DateTimeOffset.UtcNow, [])]);
        SetPrivateField(component, "_selectedModel", CreateModelRun("PC-2026-07", "M-001"));
        SetPrivateField(component, "_notesTriggerElementId", "notes-action-PC-2026-07-M-001");
        SetPrivateField(component, "_notesRequestSource", new CancellationTokenSource());

        await InvokePrivateTask(component, "NotesOpenChanged", false);

        Assert.IsFalse(GetPrivateField<bool>(component, "_notesOpen"));
        Assert.IsFalse(GetPrivateField<bool>(component, "_loadingNotes"));
        Assert.IsFalse(GetPrivateField<bool>(component, "_postingNote"));
        Assert.IsFalse(GetPrivateField<bool>(component, "_notesError"));
        Assert.IsNull(GetPrivateField<ModelRun?>(component, "_selectedModel"));
        Assert.AreEqual(string.Empty, GetPrivateField<string>(component, "_noteDraft"));
        Assert.AreEqual(0, GetPrivateField<IReadOnlyList<NoteMention>>(component, "_noteMentions").Count);
        Assert.AreEqual(0, GetPrivateField<IReadOnlyList<ModelNote>>(component, "_notes").Count);
        Assert.IsTrue(jsRuntime.Invocations.Contains("modelHistory.setBodyScrollLocked"));
        Assert.IsTrue(jsRuntime.Invocations.Contains("modelHistory.focusById"));
    }

    [TestMethod]
    public void IsActiveNotesRequest_MatchingModelAndVersion_ReturnsExpectedFlag()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_selectedModel", selected);
        SetPrivateField(component, "_notesRequestVersion", 3);

        bool active = (bool)InvokePrivate(component, "IsActiveNotesRequest", 3, selected)!;
        bool inactive = (bool)InvokePrivate(component, "IsActiveNotesRequest", 2, selected)!;

        Assert.IsTrue(active);
        Assert.IsFalse(inactive);
    }

    [TestMethod]
    public void SortChanged_NonAdditiveRequest_ResetsSortsAndPage()
    {
        ModelHistory component = new();
        IReadOnlyList<ModelRun> models =
        [
            CreateModelRun("PC-2026-07", "M-002"),
            CreateModelRun("PC-2026-07", "M-001")
        ];

        SetPrivateField(component, "_allModels", models);
        SetPrivateField(component, "_page", 2);

        InvokePrivateVoid(component, "SortChanged", new SortRequest("Id", false));

        int page = GetPrivateField<int>(component, "_page");
        object sorts = GetPrivateField<object>(component, "_sorts");
        int sortCount = (int)sorts.GetType().GetProperty("Count")!.GetValue(sorts)!;

        Assert.AreEqual(1, page);
        Assert.AreEqual(1, sortCount);
    }

    [TestMethod]
    public void AriaSort_NoSort_ReturnsNone_ThenDescendingAfterSecondSort()
    {
        ModelHistory component = new();
        SetPrivateField(component, "_allModels", (IReadOnlyList<ModelRun>)
        [
            CreateModelRun("PC-2026-07", "M-002"),
            CreateModelRun("PC-2026-07", "M-001")
        ]);

        string initial = (string)InvokePrivate(component, "AriaSort", "Id")!;
        InvokePrivateVoid(component, "SortChanged", new SortRequest("Id", false));
        InvokePrivateVoid(component, "SortChanged", new SortRequest("Id", true));
        string afterSecondSort = (string)InvokePrivate(component, "AriaSort", "Id")!;

        Assert.AreEqual("none", initial);
        Assert.AreEqual("descending", afterSecondSort);
    }

    [TestMethod]
    public void SetPage_ClampsToPageCount()
    {
        ModelHistory component = new();
        List<ModelRun> models = [];
        for (int index = 1; index <= 25; index++)
        {
            models.Add(CreateModelRun("PC-2026-07", $"M-{index:000}"));
        }

        SetPrivateField(component, "_allModels", (IReadOnlyList<ModelRun>)models);
        InvokePrivateVoid(component, "SortChanged", new SortRequest("Id", false));

        InvokePrivateVoid(component, "SetPage", 99);

        Assert.AreEqual(3, GetPrivateField<int>(component, "_page"));
    }

    [TestMethod]
    public void SearchChanged_ResetsPageAndFiltersResults()
    {
        ModelHistory component = new();
        IReadOnlyList<ModelRun> models =
        [
            CreateModelRun("PC-2026-07", "AAA-001"),
            CreateModelRun("PC-2026-07", "BBB-001")
        ];

        SetPrivateField(component, "_allModels", models);
        SetPrivateField(component, "_page", 2);

        InvokePrivateVoid(component, "SearchChanged", "AAA");

        IReadOnlyList<ModelRun> filtered = GetPrivateField<IReadOnlyList<ModelRun>>(component, "_filtered");
        Assert.AreEqual(1, GetPrivateField<int>(component, "_page"));
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("AAA-001", filtered[0].Id);
    }

    [TestMethod]
    public void TabChanged_ResetsPageAndAppliesModelTypeFilter()
    {
        ModelHistory component = new();
        IReadOnlyList<ModelRun> models =
        [
            CreateModelRun("PC-2026-07", "M-001") with { ModelType = "Beta" },
            CreateModelRun("PC-2026-07", "M-002") with { ModelType = "Alpha" }
        ];

        SetPrivateField(component, "_allModels", models);
        SetPrivateField(component, "_page", 2);

        InvokePrivateVoid(component, "TabChanged", 1);

        IReadOnlyList<ModelRun> filtered = GetPrivateField<IReadOnlyList<ModelRun>>(component, "_filtered");
        Assert.AreEqual(1, GetPrivateField<int>(component, "_page"));
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("Alpha", filtered[0].ModelType);
    }

    [TestMethod]
    public async Task LoadNotesAsync_ValidResponse_SortsNotesAndClearsLoadingState()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new
                    {
                        id = "n-1",
                        modelId = "M-001",
                        noteText = "older",
                        createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                        author = new { displayName = "User", userPrincipalName = "user@contoso.com" },
                        mentions = Array.Empty<object>()
                    },
                    new
                    {
                        id = "n-2",
                        modelId = "M-001",
                        noteText = "newer",
                        createdAtUtc = DateTimeOffset.UtcNow,
                        author = new { displayName = "User", userPrincipalName = "user@contoso.com" },
                        mentions = Array.Empty<object>()
                    }
                })
            })));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_selectedModel", selected);

        await InvokePrivateTask(component, "LoadNotesAsync", selected);

        IReadOnlyList<ModelNote> notes = GetPrivateField<IReadOnlyList<ModelNote>>(component, "_notes");
        Assert.AreEqual(2, notes.Count);
        Assert.AreEqual("n-2", notes[0].NoteId);
        Assert.IsFalse(GetPrivateField<bool>(component, "_loadingNotes"));
        Assert.IsFalse(GetPrivateField<bool>(component, "_notesError"));
    }

    [TestMethod]
    public async Task LoadNotesAsync_HttpError_SetsErrorFlagAndStopsLoading()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_notesOpen", true);
        SetPrivateField(component, "_selectedModel", selected);

        await InvokePrivateTask(component, "LoadNotesAsync", selected);

        Assert.IsTrue(GetPrivateField<bool>(component, "_notesError"));
        Assert.IsFalse(GetPrivateField<bool>(component, "_loadingNotes"));
    }

    [TestMethod]
    public async Task PostNoteAsync_HttpBadRequest_ResetsPostingAndSendsTrimmedPayload()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");
        string? requestJson = null;

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient(async (request, _) =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_selectedModel", selected);
        SetPrivateField(component, "_noteDraft", "  hello world  ");
        SetPrivateField(component, "_noteMentions", (IReadOnlyList<NoteMention>)[
            new NoteMention("u-1", "User One", "user1@contoso.com", 2, 5)
        ]);

        await InvokePrivateTask(component, "PostNoteAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_postingNote"));
        Assert.IsNotNull(requestJson);
        StringAssert.Contains(requestJson, "\"noteText\":\"hello world\"");
        StringAssert.Contains(requestJson, "\"start\":0");
    }

    [TestMethod]
    public async Task PostNoteAsync_WhitespaceDraft_DoesNotStartPosting()
    {
        ModelHistory component = new();
        ModelRun selected = CreateModelRun("PC-2026-07", "M-001");

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetPrivateField(component, "_selectedModel", selected);
        SetPrivateField(component, "_noteDraft", "    ");

        await InvokePrivateTask(component, "PostNoteAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_postingNote"));
    }

    [TestMethod]
    public async Task LoadVisibleCyclesAsync_ApiFailure_SetsHasErrorFlag()
    {
        ModelHistory component = new();

        SetInjectedProperty(component, "PlanningCyclesApiClient", CreatePlanningCyclesApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);

        await InvokePrivateTask(component, "LoadVisibleCyclesAsync");

        Assert.IsTrue(GetPrivateField<bool>(component, "_hasError"));
    }

    [TestMethod]
    public async Task LoadModelsForSelectedPeriodAsync_MissingCycleId_ClearsModelsAndEndsLoading()
    {
        ModelHistory component = new();
        DateOnly period = new(2026, 7, 1);
        Dictionary<DateOnly, PlanningCycle> cycles = GetPrivateField<Dictionary<DateOnly, PlanningCycle>>(component, "_cycles");

        cycles[period] = new PlanningCycle(string.Empty, period, 0, null, null, null);
        SetPrivateField(component, "_period", period);
        SetPrivateField(component, "_allModels", (IReadOnlyList<ModelRun>)[CreateModelRun("PC-2026-07", "M-001")]);

        await InvokePrivateTask(component, "LoadModelsForSelectedPeriodAsync");

        Assert.AreEqual(0, GetPrivateField<IReadOnlyList<ModelRun>>(component, "_allModels").Count);
        Assert.IsFalse(GetPrivateField<bool>(component, "_loadingModels"));
    }

    [TestMethod]
    public async Task CreateModelAsync_Success_ResetsViewStateAndReloadsModels()
    {
        ModelHistory component = new();
        DateOnly period = new(2026, 7, 1);
        Dictionary<DateOnly, PlanningCycle> cycles = GetPrivateField<Dictionary<DateOnly, PlanningCycle>>(component, "_cycles");

        cycles[period] = new PlanningCycle("PC-2026-07", period, 0, null, null, null);
        SetPrivateField(component, "_period", period);
        SetPrivateField(component, "_createOpen", true);
        SetPrivateField(component, "_search", "old-search");
        SetPrivateField(component, "_tabIndex", 2);
        SetPrivateField(component, "_page", 4);

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((request, _) =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.Contains("/models", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        modelId = "M-NEW",
                        createdOn = new DateOnly(2026, 7, 2),
                        status = "Draft",
                        scope = new { type = "Refinery", name = "TX" },
                        modelType = "Type-A",
                        ethanePriceUsdPerGal = 1.1m,
                        naturalGasPriceUsdPerMmbtu = 2.2m,
                        averageRecoveryPercent = 70m,
                        expectedValueMonthlyUsd = 210000m
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new
                    {
                        modelId = "M-NEW",
                        createdOn = new DateOnly(2026, 7, 2),
                        status = "Draft",
                        scope = new { type = "Refinery", name = "TX" },
                        modelType = "Type-A",
                        ethanePriceUsdPerGal = 1.1m,
                        naturalGasPriceUsdPerMmbtu = 2.2m,
                        averageRecoveryPercent = 70m,
                        expectedValueMonthlyUsd = 210000m
                    }
                })
            });
        }));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);

        await InvokePrivateTask(component, "CreateModelAsync", new ModelUpsertRequest
        {
            Status = "Draft",
            ScopeType = "Refinery",
            ScopeName = "TX",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.1m,
            NaturalGasPriceUsdPerMmbtu = 2.2m
        });

        Assert.IsFalse(GetPrivateField<bool>(component, "_createOpen"));
        Assert.AreEqual(string.Empty, GetPrivateField<string>(component, "_search"));
        Assert.AreEqual(0, GetPrivateField<int>(component, "_tabIndex"));
        Assert.AreEqual(1, GetPrivateField<int>(component, "_page"));
        Assert.AreEqual(1, GetPrivateField<IReadOnlyList<ModelRun>>(component, "_allModels").Count);
    }

    [TestMethod]
    public async Task CreateModelAsync_ApiFailure_KeepsCreateDrawerOpen()
    {
        ModelHistory component = new();
        DateOnly period = new(2026, 7, 1);
        Dictionary<DateOnly, PlanningCycle> cycles = GetPrivateField<Dictionary<DateOnly, PlanningCycle>>(component, "_cycles");

        cycles[period] = new PlanningCycle("PC-2026-07", period, 0, null, null, null);
        SetPrivateField(component, "_period", period);
        SetPrivateField(component, "_createOpen", true);

        SetInjectedProperty(component, "ModelsApiClient", CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))));
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);

        await InvokePrivateTask(component, "CreateModelAsync", new ModelUpsertRequest
        {
            Status = "Draft",
            ScopeType = "Refinery",
            ScopeName = "TX",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.1m,
            NaturalGasPriceUsdPerMmbtu = 2.2m
        });

        Assert.IsTrue(GetPrivateField<bool>(component, "_createOpen"));
    }

    private static ModelRun CreateModelRun(string planningCycleId, string modelId)
    {
        return new ModelRun(
            modelId,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            ModelStatus.Draft,
            "Refinery",
            "TX",
            "Type-A",
            1.2m,
            2.3m,
            70,
            210,
            planningCycleId);
    }

    private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = GetMethod(instance.GetType(), methodName);
        Task task = (Task)method.Invoke(instance, args)!;
        await task;
    }

    private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = GetMethod(instance.GetType(), methodName);
        method.Invoke(instance, args);
    }

    private static object? InvokePrivate(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = GetMethod(instance.GetType(), methodName);
        return method.Invoke(instance, args);
    }

    private static MethodInfo GetMethod(Type type, string methodName)
    {
        MethodInfo? method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
        return method!;
    }

    private static void SetInjectedProperty(object instance, string propertyName, object value)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(property, $"Property '{propertyName}' was not found.");
        property!.SetValue(instance, value);
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

    private static T GetStaticField<T>(string fieldName)
    {
        FieldInfo? field = typeof(ModelHistory).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Static field '{fieldName}' was not found.");
        return (T)field!.GetValue(null)!;
    }

    private static ModelsApiClient CreateModelsApiClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        TestAuthenticationStateProvider authProvider = new();
        NavigationManager navigationManager = new TestNavigationManager();
        DstApiAuthenticatedRequestSender requestSender = new(authProvider, navigationManager);
        HttpClient httpClient = new(new RecordingHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://localhost")
        };

        return new ModelsApiClient(httpClient, requestSender, NullLogger<ModelsApiClient>.Instance);
    }

    private static PlanningCyclesApiClient CreatePlanningCyclesApiClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        TestAuthenticationStateProvider authProvider = new();
        NavigationManager navigationManager = new TestNavigationManager();
        DstApiAuthenticatedRequestSender requestSender = new(authProvider, navigationManager);
        HttpClient httpClient = new(new RecordingHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://localhost")
        };

        return new PlanningCyclesApiClient(httpClient, requestSender, NullLogger<PlanningCyclesApiClient>.Instance);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        string query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (string segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = segment.Split('=', 2);
            if (parts.Length == 2)
            {
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
        }

        return result;
    }

    private sealed class TestJsRuntime : IJSRuntime
    {
        public List<string> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add(identifier);
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add(identifier);
            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Name, "Test User")
            ],
            authenticationType: "Test");

            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/model-history");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
