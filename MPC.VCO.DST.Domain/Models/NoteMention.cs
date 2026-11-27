namespace MPC.VCO.DST.Domain.Models;

public sealed class NoteMention
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public int Start { get; init; }

    public int Length { get; init; }
}
