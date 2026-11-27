using MPC.VCO.DST.Application.DTOs.Request;

namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class CreateModelNoteRequest
{
    public string ModelId { get; init; } = string.Empty;

    public string NoteText { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string UserPrincipalName { get; init; } = string.Empty;

    public string Initials { get; init; } = string.Empty;

    public IReadOnlyList<NoteMentionRequest> Mentions { get; init; } = [];
}
