using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services.ModelNotes;
using MudBlazor;
using System.Globalization;

namespace MPC.VCO.DST.WebSite.Components.Pages.Shared
{
    public partial class ModelNotesDrawer
    {
        [Parameter] public bool Open { get; set; }
        [Parameter] public EventCallback<bool> OpenChanged { get; set; }
        [Parameter] public ModelRun? Model { get; set; }
        [Parameter] public bool Loading { get; set; }
        [Parameter] public bool LoadingError { get; set; }
        [Parameter] public IReadOnlyList<ModelNote> Notes { get; set; } = [];
        [Inject] private IMentionUserLookupService MentionUserLookupService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private ILogger<ModelNotesDrawer> Logger { get; set; } = default!;

        [Parameter] public string NoteDraft { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> NoteDraftChanged { get; set; }
        [Parameter] public IReadOnlyList<NoteMention> Mentions { get; set; } = [];
        [Parameter] public EventCallback<IReadOnlyList<NoteMention>> MentionsChanged { get; set; }
        [Parameter] public bool Posting { get; set; }
        [Parameter] public EventCallback RetryRequested { get; set; }
        [Parameter] public EventCallback PostRequested { get; set; }

        private const int MentionDebounceMilliseconds = 250;
        private const int MaxMentionResults = 10;
        private ElementReference _noteInput;
        private CancellationTokenSource? _mentionSearchSource;
        private DotNetObjectReference<ModelNotesDrawer>? _selfReference;
        private IReadOnlyList<MentionUser> _mentionUsers = [];
        private string _activeMentionQuery = string.Empty;
        private int _mentionQueryStart;
        private int _mentionCaret;
        private int _selectedMentionIndex;
        private bool _showMentionSuggestions;
        private bool _mentionLoading;
        private bool _mentionError;

        private bool CanPost => !Posting && !string.IsNullOrWhiteSpace(NoteDraft);

        private string? GetActiveMentionOptionId() =>
            _showMentionSuggestions && _mentionUsers.Count > 0
                ? $"mention-option-{_selectedMentionIndex}"
                : null;

        private Color StatusColor => Model?.Status switch
        {
            ModelStatus.Executable => Color.Success,
            ModelStatus.Reviewed => Color.Warning,
            _ => Color.Default
        };

        private Task Close() => OpenChanged.InvokeAsync(false);

        private Task HandleKey(KeyboardEventArgs args) => args.Key == "Escape" ? Close() : Task.CompletedTask;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _selfReference = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("modelHistory.attachMentionInput", _noteInput, _selfReference);
            }
        }

        public async ValueTask DisposeAsync()
        {
            CancelMentionSearch();
            if (_selfReference is not null)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("modelHistory.detachMentionInput", _noteInput);
                }
                catch (JSDisconnectedException ex)
                {
                    Logger.LogDebug(
                     ex,
                     "JS runtime disconnected while detaching the mention input during component disposal.");
                }

                _selfReference.Dispose();
            }
        }

        private async Task HandleNoteInput(ChangeEventArgs args)
        {
            string nextDraft = args.Value?.ToString() ?? string.Empty;
            int caret = await JSRuntime.InvokeAsync<int>("modelHistory.getNoteCaret", _noteInput);
            IReadOnlyList<NoteMention> updatedMentions =
                ModelNotesMentionHelper.AdjustMentionOffsets(NoteDraft, nextDraft, Mentions);

            await NoteDraftChanged.InvokeAsync(nextDraft);
            await MentionsChanged.InvokeAsync(updatedMentions);

            _mentionCaret = Math.Clamp(caret, 0, nextDraft.Length);
            await UpdateMentionSuggestionsAsync(nextDraft, _mentionCaret);
        }

        [JSInvokable]
        public async Task HandleMentionKey(string key)
        {
            if (!_showMentionSuggestions)
            {
                return;
            }

            switch (key)
            {
                case "ArrowDown":
                    if (_mentionUsers.Count > 0)
                    {
                        _selectedMentionIndex = (_selectedMentionIndex + 1) % _mentionUsers.Count;
                    }
                    break;
                case "ArrowUp":
                    if (_mentionUsers.Count > 0)
                    {
                        _selectedMentionIndex = (_selectedMentionIndex - 1 + _mentionUsers.Count) % _mentionUsers.Count;
                    }
                    break;
                case "Enter":
                    if (!_mentionLoading && !_mentionError && _selectedMentionIndex >= 0 && _selectedMentionIndex < _mentionUsers.Count)
                    {
                        await SelectMentionAsync(_mentionUsers[_selectedMentionIndex]);
                    }
                    break;
                case "Escape":
                    CloseMentionSuggestions();
                    break;
            }

            await InvokeAsync(StateHasChanged);
        }

        private Task UpdateMentionSuggestionsAsync(string text, int caret)
        {
            if (!ModelNotesMentionHelper.TryGetMentionQueryContext(
                    text,
                    caret,
                    out MentionQueryContext? queryContext)
                || queryContext is null)
            {
                CloseMentionSuggestions();
                return Task.CompletedTask;
            }

            _mentionQueryStart = queryContext.QueryStart;
            _mentionCaret = queryContext.Caret;
            _activeMentionQuery = queryContext.Query;
            _showMentionSuggestions = true;
            _mentionLoading = true;
            _mentionError = false;
            _mentionUsers = [];
            _selectedMentionIndex = 0;
            CancelMentionSearch();
            _mentionSearchSource = new CancellationTokenSource();
            _ = SearchMentionUsersAsync(queryContext.Query, _mentionSearchSource.Token, debounce: true);
            return Task.CompletedTask;
        }

        private async Task SearchMentionUsersAsync(
            string query,
            CancellationToken cancellationToken,
            bool debounce)
        {
            try
            {
                if (debounce)
                {
                    await Task.Delay(MentionDebounceMilliseconds, cancellationToken);
                }

                IReadOnlyList<MentionUser> users = await MentionUserLookupService.SearchAsync(query, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    _mentionUsers = users.Take(MaxMentionResults).ToArray();
                    _mentionLoading = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _mentionError = true;
                    _mentionLoading = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (HttpRequestException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _mentionError = true;
                    _mentionLoading = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (InvalidOperationException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _mentionError = true;
                    _mentionLoading = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
        }

        private Task RetryMentionSearchAsync()
        {
            _mentionLoading = true;
            _mentionError = false;
            CancelMentionSearch();
            _mentionSearchSource = new CancellationTokenSource();
            return SearchMentionUsersAsync(_activeMentionQuery, _mentionSearchSource.Token, debounce: false);
        }

        private async Task SelectMentionAsync(MentionUser user)
        {
            MentionSelectionResult selection =
                ModelNotesMentionHelper.ApplyMentionSelection(
                    NoteDraft,
                    Mentions,
                    user,
                    _mentionQueryStart,
                    _mentionCaret);

            CloseMentionSuggestions();
            await NoteDraftChanged.InvokeAsync(selection.NextDraft);
            await MentionsChanged.InvokeAsync(selection.UpdatedMentions);
            await InvokeAsync(StateHasChanged);
            await JSRuntime.InvokeVoidAsync("modelHistory.setNoteCaret", _noteInput, selection.NextCaret);
        }

        private void CloseMentionSuggestions()
        {
            _showMentionSuggestions = false;
            _mentionLoading = false;
            _mentionError = false;
            _mentionUsers = [];
            CancelMentionSearch();
        }

        private void CancelMentionSearch()
        {
            _mentionSearchSource?.Cancel();
            _mentionSearchSource?.Dispose();
            _mentionSearchSource = null;
        }

        private static IReadOnlyList<NoteTextSegment> GetNoteTextSegments(ModelNote note)
        {
            return ModelNoteTextSegmentationHelper.GetNoteTextSegments(note);
        }

        private static string FormatDate(DateOnly value) => value == default
            ? "—"
            : value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

        private static string FormatScope(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

        private static string FormatCurrency(decimal value, string suffix)
        {
            return value <= 0 ? "—" : $"${value:0.00}{suffix}";
        }

        private static string FormatRecovery(int? value) => value is null ? "—" : $"{value}%";

        private static string FormatExpected(int? value)
        {
            if (value is null)
            {
                return "—";
            }

            string sign = value.Value > 0 ? "+" : string.Empty;
            return $"{sign}${value}K/mo";
        }

        private static string GetExpectedValueClass(int? value) => value is > 0 ? "positive-value" : string.Empty;

        private static string FormatName(string fullName) => string.IsNullOrWhiteSpace(fullName) ? "Unknown User" : fullName;

        private static string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "U";
            }

            string[] segments = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length == 0)
            {
                return "U";
            }

            if (segments.Length == 1)
            {
                return segments[0][0].ToString().ToUpperInvariant();
            }

            return string.Concat(segments[0][0], segments[^1][0]).ToUpperInvariant();
        }
    }
}
