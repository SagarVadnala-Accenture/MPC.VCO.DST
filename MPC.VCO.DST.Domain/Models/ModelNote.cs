namespace MPC.VCO.DST.Domain.Models;

public sealed class ModelNote
{
    public string Id { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string NoteText { get; init; } = string.Empty;

    public ModelNoteAuthor Author { get; init; } = new();

    public IReadOnlyList<NoteMention> Mentions { get; init; } = [];

    public DateTimeOffset CreatedAtUtc { get; init;}

    public DateTimeOffset? UpdatedAtUtc { get; init; }

    public bool IsDeleted { get; init; }

    public int SchemaVersion { get; init; }
}

public sealed class ModelNoteAuthor
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string UserPrincipalName { get; init; } = string.Empty;

    public string Initials { get; init; } = string.Empty;
}

public sealed class ModelNoteQueryResult
{
    public IReadOnlyList<ModelNote> Notes { get; init; } = [];

    public string? ContinuationToken { get; init; }
}
