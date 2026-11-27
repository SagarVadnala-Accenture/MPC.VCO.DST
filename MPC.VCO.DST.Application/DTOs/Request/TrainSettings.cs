
using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class TrainSettings
{
    [JsonPropertyName("r_min")]
    public double RMin { get; init; } = 0.05;

    [JsonPropertyName("r_max")]
    public double RMax { get; init; } = 0.92;

    [JsonPropertyName("r0")]
    public double R0 { get; init; } = 0.20;

    [JsonPropertyName("locked_at")]
    public double? LockedAt { get; init; }

    [JsonPropertyName("c_op")]
    public double COp { get; init; } = 0.5;
}
