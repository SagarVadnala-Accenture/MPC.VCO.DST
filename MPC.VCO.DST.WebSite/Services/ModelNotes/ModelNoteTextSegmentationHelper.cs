using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services.ModelNotes;

public static class ModelNoteTextSegmentationHelper
{
    public static IReadOnlyList<NoteTextSegment> GetNoteTextSegments(ModelNote note)
    {
        List<NoteTextSegment> segments = [];
        int currentPosition = 0;

        foreach (NoteMention mention in note.Mentions.OrderBy(mention => mention.Start))
        {
            if (mention.Start < currentPosition
                || mention.Length <= 0
                || mention.Start > note.Text.Length - mention.Length)
            {
                continue;
            }

            string mentionText = note.Text.Substring(mention.Start, mention.Length);
            if (!string.Equals(mentionText, $"@{mention.DisplayName}", StringComparison.Ordinal))
            {
                continue;
            }

            if (mention.Start > currentPosition)
            {
                segments.Add(new NoteTextSegment(note.Text[currentPosition..mention.Start], false));
            }

            segments.Add(new NoteTextSegment(mentionText, true));
            currentPosition = mention.Start + mention.Length;
        }

        if (currentPosition < note.Text.Length)
        {
            segments.Add(new NoteTextSegment(note.Text[currentPosition..], false));
        }

        return segments;
    }
}

public sealed record NoteTextSegment(string Text, bool IsMention);
