namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class NoteMentionRequest
{
    public string UserId { get; init; } = string.Empty;

    public int Start { get; init; }

    public int Length { get; init; }
}
