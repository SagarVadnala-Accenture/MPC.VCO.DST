namespace MPC.VCO.DST.WebSite.Models;

public sealed record NoteMention(
    string UserId,
    string DisplayName,
    string Email,
    int Start,
    int Length);

public sealed record MentionUser(string Id, string DisplayName, string Email);

public sealed record CreateModelNoteRequest(
    string Text,
    IReadOnlyList<NoteMention> Mentions);

public sealed record ModelNote(
    string NoteId,
    string ModelId,
    string FullName,
    string? UserName,
    string Text,
    DateTimeOffset CreatedOn,
    IReadOnlyList<NoteMention> Mentions);

public sealed class ModelNoteApiResponse
{
    public string Id { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string NoteText { get; init; } = string.Empty;

    public ModelNoteAuthorApiResponse Author { get; init; } = new();

    public IReadOnlyList<ModelNoteMentionApiResponse> Mentions { get; init; } = [];

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

public sealed class ModelNoteMentionApiResponse
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public int Start { get; init; }

    public int Length { get; init; }
}

public sealed class MentionUserApiResponse
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}

public sealed class ModelNoteAuthorApiResponse
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string UserPrincipalName { get; init; } = string.Empty;

    public string Initials { get; init; } = string.Empty;
}
