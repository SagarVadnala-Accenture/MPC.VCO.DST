using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Application.DTOs.Response;

public sealed class TrainDay
{
    [JsonPropertyName("train")]
    public string Train { get; init; } = string.Empty;

    [JsonPropertyName("plant")]
    public string Plant { get; init; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; init; }

    [JsonPropertyName("date")]
    public DateOnly Date { get; init; }

    [JsonPropertyName("recovery")]
    public double Recovery { get; init; }

    [JsonPropertyName("changed")]
    public bool Changed { get; init; }
}
