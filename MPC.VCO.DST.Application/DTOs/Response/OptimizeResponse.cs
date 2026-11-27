using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Application.DTOs.Response;

public sealed class OptimizeResponse
{
    [JsonPropertyName("scenario_id")]
    public string ScenarioId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("portfolio_margin_usd")]
    public double PortfolioMarginUsd { get; init; }

    [JsonPropertyName("planning_start_date")]
    public DateOnly PlanningStartDate { get; init; }

    [JsonPropertyName("planning_end_date")]
    public DateOnly PlanningEndDate { get; init; }

    [JsonPropertyName("train_schedule")]
    public IReadOnlyList<TrainDay> TrainSchedule { get; init; } = [];

    [JsonPropertyName("plant_recovery")]
    public IReadOnlyList<PlantDay> PlantRecovery { get; init; } = [];
}
