using MPC.VCO.DST.Domain.Models;
using MPC.VCO.DST.Infrastructure.Entities;

namespace MPC.VCO.DST.Infrastructure.Mapper;

internal static class ModelNoteMapper
{
    public static ModelNote ToDomain(this ModelNoteEntity entity)
    {
        return new ModelNote
        {
            Id = entity.Id,
            ModelId = entity.ModelId,
            NoteText = entity.NoteText,
            Author = new ModelNoteAuthor
            {
                UserId = entity.Author.UserId,
                DisplayName = entity.Author.DisplayName,
                UserPrincipalName = entity.Author.UserPrincipalName,
                Initials = entity.Author.Initials
            },
            Mentions = entity.Mentions.Select(mention => new NoteMention
            {
                UserId = mention.UserId,
                DisplayName = mention.DisplayName,
                Email = mention.Email,
                Start = mention.Start,
                Length = mention.Length
            }).ToArray(),
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            IsDeleted = entity.IsDeleted,
            SchemaVersion = entity.SchemaVersion
        };
    }

    public static ModelNoteEntity ToEntity(this ModelNote note)
    {
        return new ModelNoteEntity
        {
            Id = note.Id,
            ModelId = note.ModelId,
            NoteText = note.NoteText,
            Author = new ModelNoteAuthorEntity
            {
                UserId = note.Author.UserId,
                DisplayName = note.Author.DisplayName,
                UserPrincipalName = note.Author.UserPrincipalName,
                Initials = note.Author.Initials
            },
            Mentions = note.Mentions.Select(mention => new ModelNoteMentionEntity
            {
                UserId = mention.UserId,
                DisplayName = mention.DisplayName,
                Email = mention.Email,
                Start = mention.Start,
                Length = mention.Length
            }).ToList(),
            CreatedAtUtc = note.CreatedAtUtc,
            UpdatedAtUtc = note.UpdatedAtUtc,
            IsDeleted = note.IsDeleted,
            SchemaVersion = note.SchemaVersion
        };
    }
}
