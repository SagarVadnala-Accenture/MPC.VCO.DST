using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Domain.Models;

namespace MPC.VCO.DST.Application.Mappers;

internal static class ModelNoteMapper
{
    public static ModelNoteResponse ToResponse(
        this ModelNote note)
    {
        return new ModelNoteResponse
        {
            Id = note.Id,
            ModelId = note.ModelId,
            NoteText = note.NoteText,
            Author = new ModelNoteAuthorResponse
            {
                UserId = note.Author.UserId,
                DisplayName = note.Author.DisplayName,
                UserPrincipalName = note.Author.UserPrincipalName,
                Initials = note.Author.Initials
            },
            Mentions = note.Mentions
                .Select(mention => new ModelNoteMentionResponse
                {
                    UserId = mention.UserId,
                    DisplayName = mention.DisplayName,
                    Email = mention.Email,
                    Start = mention.Start,
                    Length = mention.Length
                })
                .ToArray(),
            CreatedAtUtc = note.CreatedAtUtc,
            UpdatedAtUtc = note.UpdatedAtUtc
        };
    }
}
