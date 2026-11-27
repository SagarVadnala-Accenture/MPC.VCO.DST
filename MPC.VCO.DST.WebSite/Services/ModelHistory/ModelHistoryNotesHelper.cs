using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services.ModelHistory;

public static class ModelHistoryNotesHelper
{
    public static bool TryBuildCreateRequest(
        string noteDraft,
        IReadOnlyList<NoteMention> noteMentions,
        out CreateModelNoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(noteDraft);
        ArgumentNullException.ThrowIfNull(noteMentions);

        int leadingWhitespaceLength = noteDraft.Length - noteDraft.TrimStart().Length;
        string text = noteDraft.Trim();

        if (text.Length == 0)
        {
            request = new CreateModelNoteRequest(string.Empty, []);
            return false;
        }

        IReadOnlyList<NoteMention> mentions = noteMentions
            .Where(mention => mention.Start >= leadingWhitespaceLength)
            .Select(mention => mention with { Start = mention.Start - leadingWhitespaceLength })
            .ToArray();

        request = new CreateModelNoteRequest(text, mentions);
        return true;
    }

    public static IReadOnlyList<ModelNote> MergePostedNote(
        IReadOnlyList<ModelNote> notes,
        ModelNote postedNote)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(postedNote);

        return
        [
            postedNote,
        .. notes.Where(existing =>
            !string.Equals(existing.NoteId, postedNote.NoteId, StringComparison.Ordinal))
        ];
    }

    public static IReadOnlyList<ModelNote> SortNotes(
        IReadOnlyList<ModelNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        return notes
            .OrderByDescending(note => note.CreatedOn)
            .ThenByDescending(note => note.NoteId)
            .ToArray();
    }

    public static string GetPostNoteErrorMessage(HttpRequestException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.StatusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => "Could not post note: the note request is invalid.",
            System.Net.HttpStatusCode.Unauthorized => "Could not post note: please sign in and try again.",
            System.Net.HttpStatusCode.Forbidden => "Could not post note: you do not have permission to post notes.",
            System.Net.HttpStatusCode.NotFound => "Could not post note: the selected model was not found.",
            _ => $"Could not post note: {exception.Message}"
        };
    }
}
