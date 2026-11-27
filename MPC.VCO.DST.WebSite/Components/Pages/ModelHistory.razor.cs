using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services.ModelHistory;
using MPC.VCO.DST.WebSite.Services;
using MudBlazor;

namespace MPC.VCO.DST.WebSite.Components.Pages;

public partial class ModelHistory : IDisposable
{
    [Inject] private PlanningCyclesApiClient PlanningCyclesApiClient { get; set; } = default!;
    [Inject] private ModelsApiClient ModelsApiClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "state")] public string? PreviewState { get; set; }
    [SupplyParameterFromQuery(Name = "cycle")] public string? PreviewCycle { get; set; }

    private const int PageSize = 10;
    private static readonly DateOnly CurrentPeriod = new(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1);
    private static readonly DateOnly MinimumPeriod = CurrentPeriod.AddMonths(-12);
    private static readonly DateOnly MaximumPeriod = CurrentPeriod.AddMonths(12);

    private static readonly TableColumn[] Columns =
    [
        new("Id", "MODEL ID", 174, model => model.Id),
        new("CreatedOn", "CREATED ON", 132, model => model.CreatedOn),
        new("Status", "STATUS", 127, model => model.Status.ToString()),
        new("Scope", "SCOPE", 142, model => model.Scope + model.Location),
        new("ModelType", "MODEL TYPE", 124, model => model.ModelType),
        new("EthanePrice", "AVG. ETHANE PRICE", 135, model => model.EthanePrice),
        new("NaturalGasPrice", "AVG. NAT GAS PRICE", 139, model => model.NaturalGasPrice),
        //new("Recovery", "AVG. RECOVERY", 142, model => model.Recovery ?? -1),
        new("ExpectedValue", "AVG. EXPECTED VALUE", 151, model => model.ExpectedValue ?? -1)
    ];

    private DateOnly _period = CurrentPeriod;
    private string _search = string.Empty;
    private int _tabIndex;
    private int _page = 1;
    private bool _hasError;
    private bool _loadingModels;
    private bool _createOpen;
    private bool _notesOpen;
    private bool _loadingNotes;
    private bool _notesError;
    private bool _postingNote;
    private string _noteDraft = string.Empty;
    private IReadOnlyList<NoteMention> _noteMentions = [];
    private ModelRun? _selectedModel;
    private IReadOnlyList<ModelNote> _notes = [];
    private CancellationTokenSource? _notesRequestSource;
    private int _notesRequestVersion;
    private string? _notesTriggerElementId;
    private IReadOnlyList<ModelRun> _allModels = [];
    private IReadOnlyList<ModelRun> _filtered = [];
    private IReadOnlyList<string> _tabs = ["All Models"];
    private int _pageCount = 1;
    private readonly Dictionary<DateOnly, PlanningCycle> _cycles = [];
    private readonly List<(TableColumn Column, bool Descending)> _sorts = [];

    private bool ReadOnly => _period < CurrentPeriod;
    private bool CycleHasModels => _allModels.Count > 0;
    private int PageCount => _pageCount;
    private IEnumerable<ModelRun> VisibleRows => _filtered.Skip((_page - 1) * PageSize).Take(PageSize);
    private IEnumerable<DateOnly> VisiblePeriods => [_period.AddMonths(-1), _period, _period.AddMonths(1)];
    private IReadOnlyList<string> Tabs => _tabs;

    protected override async Task OnParametersSetAsync()
    {
        _hasError = string.Equals(PreviewState, "error", StringComparison.OrdinalIgnoreCase);

        if (DateOnly.TryParseExact(
                $"{PreviewCycle}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly period)
            && period >= MinimumPeriod
            && period <= MaximumPeriod)
        {
            _period = period;
        }

        await LoadVisibleCyclesAsync();

        if (_hasError)
        {
            return;
        }

        await LoadModelsForSelectedPeriodAsync();
        Refresh();
    }

    private static string GetNotesActionButtonId(ModelRun model)
    {
        return $"notes-action-{model.PlanningCycleId}-{model.Id}";
    }

    private async Task OpenNotesDrawerAsync(ModelRun model)
    {
        _notesTriggerElementId = GetNotesActionButtonId(model);
        _selectedModel = model;
        _notesOpen = true;
        _notesError = false;
        _noteDraft = string.Empty;
        _noteMentions = [];
        _notes = [];

        await SetBodyScrollLockAsync(true);
        await LoadNotesAsync(model);
    }

    private async Task NotesOpenChanged(bool open)
    {
        if (_notesOpen == open)
        {
            return;
        }

        _notesOpen = open;

        if (open)
        {
            await SetBodyScrollLockAsync(true);
            return;
        }

        _notesRequestVersion++;
        CancelNotesRequest();
        _loadingNotes = false;
        _postingNote = false;
        _notesError = false;
        _selectedModel = null;
        _notes = [];
        _noteDraft = string.Empty;
        _noteMentions = [];

        await SetBodyScrollLockAsync(false);

        if (!string.IsNullOrWhiteSpace(_notesTriggerElementId))
        {
            await RestoreFocusAsync(_notesTriggerElementId);
        }
    }


    private Task NoteDraftChanged(string? value)
    {
        _noteDraft = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task NoteMentionsChanged(IReadOnlyList<NoteMention> mentions)
    {
        _noteMentions = mentions;
        return Task.CompletedTask;
    }

    private async Task RetryNotesAsync()
    {
        if (_selectedModel is null)
        {
            return;
        }

        await LoadNotesAsync(_selectedModel);
    }

    private async Task PostNoteAsync()
    {
        if (_selectedModel is null || _postingNote)
        {
            return;
        }

        if (!ModelHistoryNotesHelper.TryBuildCreateRequest(
                _noteDraft,
                _noteMentions,
                out CreateModelNoteRequest request))
        {
            return;
        }

        _postingNote = true;
        int requestVersion = ++_notesRequestVersion;

        CancelNotesRequest();
        _notesRequestSource = new CancellationTokenSource();

        try
        {
            ModelNote note = await ModelsApiClient.CreateModelNoteAsync(
                _selectedModel.PlanningCycleId,
                _selectedModel.Id,
                request,
                _notesRequestSource.Token);

            if (!IsActiveNotesRequest(requestVersion, _selectedModel))
            {
                return;
            }

            _notes = ModelHistoryNotesHelper.MergePostedNote(_notes, note);

            _noteDraft = string.Empty;
            _noteMentions = [];
            await InvokeAsync(StateHasChanged);

            Snackbar.Add("Note posted.", Severity.Success);
        }
        catch (HttpRequestException exception)
        {
            Snackbar.Add(ModelHistoryNotesHelper.GetPostNoteErrorMessage(exception), Severity.Error);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or ArgumentException)
        {
            Snackbar.Add(
                $"Could not post note: {exception.Message}",
                Severity.Error);
        }
        finally
        {
            _postingNote = false;
        }
    }

    private async Task LoadNotesAsync(ModelRun model)
    {
        _loadingNotes = true;
        _notesError = false;
        _notes = [];
        int requestVersion = ++_notesRequestVersion;

        CancelNotesRequest();
        _notesRequestSource = new CancellationTokenSource();

        try
        {
            IReadOnlyList<ModelNote> notes = await ModelsApiClient.GetModelNotesAsync(
                model.PlanningCycleId,
                model.Id,
                _notesRequestSource.Token);

            if (!IsActiveNotesRequest(requestVersion, model))
            {
                return;
            }

            _notes = ModelHistoryNotesHelper.SortNotes(notes);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
            when (exception is HttpRequestException or InvalidOperationException or ArgumentException)
        {
            if (!IsActiveNotesRequest(requestVersion, model))
            {
                return;
            }

            _notesError = true;
            Snackbar.Add($"Could not load model notes: {exception.Message}", Severity.Error);
        }
        finally
        {
            if (IsActiveNotesRequest(requestVersion, model))
            {
                _loadingNotes = false;
            }
        }
    }

    private bool IsActiveNotesRequest(int requestVersion, ModelRun model)
    {
        return _notesOpen
            && _selectedModel is not null
            && string.Equals(_selectedModel.PlanningCycleId, model.PlanningCycleId, StringComparison.Ordinal)
            && string.Equals(_selectedModel.Id, model.Id, StringComparison.Ordinal)
            && requestVersion == _notesRequestVersion;
    }

    private void CancelNotesRequest()
    {
        _notesRequestSource?.Cancel();
        _notesRequestSource?.Dispose();
        _notesRequestSource = null;
    }

    private ValueTask SetBodyScrollLockAsync(bool locked)
    {
        return JSRuntime.InvokeVoidAsync("modelHistory.setBodyScrollLocked", locked);
    }

    private ValueTask RestoreFocusAsync(string elementId)
    {
        return JSRuntime.InvokeVoidAsync("modelHistory.focusById", elementId);
    }

    private void OpenCreateModel()
    {
        if (!ReadOnly)
        {
            _createOpen = true;
        }
    }

    private void CreateOpenChanged(bool open)
    {
        _createOpen = open;
    }

    private async Task CreateModelAsync(ModelUpsertRequest request)
    {
        try
        {
            PlanningCycle cycle = GetCycle(_period);

            if (string.IsNullOrWhiteSpace(cycle.PlanningCycleId))
            {
                Snackbar.Add(
                    "Cannot create model because this planning cycle is not available in the API.",
                    Severity.Warning);
                return;
            }

            ModelRun model = await ModelsApiClient.CreateModelAsync(cycle.PlanningCycleId, request);

            _createOpen = false;
            _search = string.Empty;
            _tabIndex = 0;
            _page = 1;
            _sorts.Clear();

            await LoadModelsForSelectedPeriodAsync();
            Refresh();

            Snackbar.Add($"{model.Id} created.", Severity.Success);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or InvalidOperationException or ArgumentException)
        {
            Snackbar.Add($"Could not create model: {exception.Message}", Severity.Error);
        }
    }

    private PlanningCycle GetCycle(DateOnly period)
    {
        return _cycles.TryGetValue(period, out PlanningCycle? cycle)
            ? cycle
            : new PlanningCycle(string.Empty, period, 0, null, null, null);
    }

    private async Task LoadVisibleCyclesAsync()
    {
        foreach (DateOnly period in VisiblePeriods)
        {
            if (_cycles.ContainsKey(period))
            {
                continue;
            }

            try
            {
                _cycles[period] = await PlanningCyclesApiClient.GetPlanningCycleAsync(period);
            }
            catch (Exception)
            {
                _hasError = true;
                Snackbar.Add("Could not load planning cycle data from API.", Severity.Error);
                return;
            }
        }
    }

    private async Task LoadModelsForSelectedPeriodAsync()
    {
        _loadingModels = true;

        try
        {
            PlanningCycle cycle = GetCycle(_period);

            if (string.IsNullOrWhiteSpace(cycle.PlanningCycleId))
            {
                _allModels = [];
                return;
            }

            _allModels = await ModelsApiClient.GetModelsAsync(cycle);
            _cycles[_period] = cycle with { ModelCount = _allModels.Count };
        }
        catch (Exception exception)
            when (exception is HttpRequestException or InvalidOperationException)
        {
            _hasError = true;
            _allModels = [];
            Snackbar.Add("Could not load model history data from API.", Severity.Error);
        }
        finally
        {
            _loadingModels = false;
        }
    }

    private void Refresh()
    {
        ModelHistorySortDefinition[] sortDefinitions = _sorts
            .Select(sort => new ModelHistorySortDefinition(sort.Column.Selector, sort.Descending))
            .ToArray();

        ModelHistoryTableState tableState = ModelHistoryTableStateHelper.Build(
            _allModels,
            _search,
            _tabIndex,
            sortDefinitions,
            _page,
            PageSize);

        _tabs = tableState.Tabs;
        _tabIndex = tableState.TabIndex;
        _filtered = tableState.Filtered;
        _page = tableState.Page;
        _pageCount = tableState.PageCount;
    }

    private void SearchChanged(string value) { _search = value; _page = 1; Refresh(); }
    private void TabChanged(int index) { _tabIndex = index; _page = 1; Refresh(); }
    private void SetPage(int page) => _page = Math.Clamp(page, 1, PageCount);

    private async Task SelectPeriod(DateOnly period)
    {
        if (period < MinimumPeriod || period > MaximumPeriod)
        {
            return;
        }

        _period = period;
        _page = 1;
        _tabIndex = 0;
        _search = string.Empty;
        _sorts.Clear();

        await LoadVisibleCyclesAsync();

        if (_hasError)
        {
            return;
        }

        await LoadModelsForSelectedPeriodAsync();
        Refresh();
    }

    private void SortChanged(SortRequest request)
    {
        TableColumn column = Columns.Single(column => column.Key == request.Column);
        int existing = _sorts.FindIndex(sort => sort.Column.Key == request.Column);
        bool descending = existing >= 0 && !_sorts[existing].Descending;

        if (!request.Additive)
        {
            _sorts.Clear();
        }
        else if (existing >= 0)
        {
            _sorts.RemoveAt(existing);
        }

        _sorts.Add((column, descending));
        _page = 1;
        Refresh();
    }

    private bool IsSorted(string key) => _sorts.Any(sort => sort.Column.Key == key);

    private string AriaSort(string key)
    {
        var sort = _sorts.Find(sort => sort.Column.Key == key);
        return sort.Column is null ? "none" : sort.Descending ? "descending" : "ascending";
    }

    private static Color GetStatusColor(ModelStatus status) => status switch
    {
        ModelStatus.Executable => Color.Success,
        ModelStatus.Reviewed => Color.Warning,
        _ => Color.Default
    };

    private async Task Retry()
    {
        _hasError = false;
        _cycles.Clear();
        _allModels = [];

        await LoadVisibleCyclesAsync();

        if (_hasError)
        {
            return;
        }

        await LoadModelsForSelectedPeriodAsync();
        Refresh();
    }

    private sealed record TableColumn(string Key, string Title, int Width, Func<ModelRun, IComparable> Selector);

    public void Dispose()
    {
        CancelNotesRequest();
    }
}
