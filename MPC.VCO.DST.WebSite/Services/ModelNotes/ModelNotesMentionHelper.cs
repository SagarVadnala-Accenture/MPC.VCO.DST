using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services.ModelNotes;

public static class ModelNotesMentionHelper
{
    public static bool TryGetMentionQueryContext(
        string text,
        int caret,
        out MentionQueryContext? context)
    {
        context = null;

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        int normalizedCaret = Math.Clamp(caret, 0, text.Length);
        string prefix = text[..normalizedCaret];
        int atIndex = prefix.LastIndexOf('@');

        if (atIndex < 0
            || (atIndex > 0 && !char.IsWhiteSpace(prefix[atIndex - 1])))
        {
            return false;
        }

        string query = prefix[(atIndex + 1)..];

        if (query.Any(character => char.IsWhiteSpace(character) || character == '@'))
        {
            return false;
        }

        context = new MentionQueryContext(atIndex, normalizedCaret, query);
        return true;
    }

    public static IReadOnlyList<NoteMention> AdjustMentionOffsets(
        string previousText,
        string nextText,
        IReadOnlyList<NoteMention> mentions)
    {
        int prefixLength = 0;
        while (prefixLength < previousText.Length
            && prefixLength < nextText.Length
            && previousText[prefixLength] == nextText[prefixLength])
        {
            prefixLength++;
        }

        int previousSuffix = previousText.Length;
        int nextSuffix = nextText.Length;
        while (previousSuffix > prefixLength
            && nextSuffix > prefixLength
            && previousText[previousSuffix - 1] == nextText[nextSuffix - 1])
        {
            previousSuffix--;
            nextSuffix--;
        }

        int delta = (nextSuffix - prefixLength) - (previousSuffix - prefixLength);
        List<NoteMention> adjusted = [];
        foreach (NoteMention mention in mentions)
        {
            int end = mention.Start + mention.Length;
            if (end <= prefixLength)
            {
                adjusted.Add(mention);
            }
            else if (mention.Start >= previousSuffix)
            {
                adjusted.Add(mention with { Start = mention.Start + delta });
            }
        }

        return adjusted;
    }

    public static MentionSelectionResult ApplyMentionSelection(
        string draft,
        IReadOnlyList<NoteMention> mentions,
        MentionUser user,
        int mentionQueryStart,
        int mentionCaret)
    {
        string insertion = $"@{user.DisplayName} ";
        string nextDraft = string.Concat(
            draft.AsSpan(0, mentionQueryStart),
            insertion,
            draft.AsSpan(mentionCaret));
        int mentionLength = insertion.Length - 1;

        IReadOnlyList<NoteMention> updatedMentions = AdjustMentionOffsets(draft, nextDraft, mentions)
            .Where(mention => mention.Start != mentionQueryStart)
            .Append(new NoteMention(user.Id, user.DisplayName, user.Email, mentionQueryStart, mentionLength))
            .ToArray();

        int nextCaret = mentionQueryStart + insertion.Length;

        return new MentionSelectionResult(nextDraft, updatedMentions, nextCaret);
    }
}

public sealed record MentionQueryContext(
    int QueryStart,
    int Caret,
    string Query);

public sealed record MentionSelectionResult(
    string NextDraft,
    IReadOnlyList<NoteMention> UpdatedMentions,
    int NextCaret);
