namespace MPC.VCO.DST.Application.DTOs.Response;

public sealed class ModelNoteResponse
{
    public string Id { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string NoteText { get; init; } = string.Empty;

    public ModelNoteAuthorResponse Author { get; init; } = new();

    public IReadOnlyList<ModelNoteMentionResponse> Mentions { get; init; } = [];

    public DateTimeOffset CreatedAtUtc { get; init;}

    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

public sealed class ModelNoteAuthorResponse
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string UserPrincipalName { get; init; } = string.Empty;

    public string Initials { get; init; } = string.Empty;
}

public sealed class ModelNoteMentionResponse
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public int Start { get; init; }

    public int Length { get; init; }
}

public sealed class GetModelNotesResponse
{
    public IReadOnlyList<ModelNoteResponse> Notes { get; init; } = [];

    public string? ContinuationToken { get; init; }
}
