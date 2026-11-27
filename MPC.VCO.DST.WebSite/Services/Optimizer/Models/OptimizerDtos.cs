using System.Text.Json.Serialization;

namespace MPC.VCO.DST.WebSite.Services.Optimizer.Models;

public sealed class OptimizeRequestDto
{
    [JsonPropertyName("planning_start_date")]
    public DateOnly? PlanningStartDate { get; set; }

    [JsonPropertyName("planning_end_date")]
    public DateOnly? PlanningEndDate { get; set; }

    [JsonPropertyName("horizon_days")]
    public int HorizonDays { get; set; }

    [JsonPropertyName("ethane_price_shift_pct")]
    public double EthanePriceShiftPct { get; set; }

    [JsonPropertyName("gas_price_shift_pct")]
    public double GasPriceShiftPct { get; set; }

    [JsonPropertyName("tf_fee")]
    public double TfFee { get; set; } = 4.0;

    [JsonPropertyName("ethane_heating_value")]
    public double EthaneHeatingValue { get; set; } = 2.8;

    [JsonPropertyName("takeaway_bbl_per_day")]
    public double? TakeawayBblPerDay { get; set; }

    [JsonPropertyName("k_fix")]
    public double KFix { get; set; } = 5000.0;

    [JsonPropertyName("k_var")]
    public double KVar { get; set; } = 200000.0;

    [JsonPropertyName("max_changes_per_train")]
    public int MaxChangesPerTrain { get; set; } = 4;

    [JsonPropertyName("min_hold_days")]
    public int MinHoldDays { get; set; } = 1;

    [JsonPropertyName("time_limit_sec")]
    public int TimeLimitSec { get; set; } = 120;

    [JsonPropertyName("mip_gap")]
    public double MipGap { get; set; } = 0.01;

    [JsonPropertyName("trains")]
    public Dictionary<string, TrainSettingsDto> Trains { get; set; } = [];
}

public sealed class TrainSettingsDto
{
    [JsonPropertyName("r_min")]
    public double RMin { get; set; } = 0.05;

    [JsonPropertyName("r_max")]
    public double RMax { get; set; } = 0.92;

    [JsonPropertyName("r0")]
    public double R0 { get; set; } = 0.20;

    [JsonPropertyName("locked_at")]
    public double? LockedAt { get; set; }

    [JsonPropertyName("c_op")]
    public double COp { get; set; } = 0.5;
}

public sealed class OptimizeResponseDto
{
    [JsonPropertyName("scenario_id")]
    public string ScenarioId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("portfolio_margin_usd")]
    public double PortfolioMarginUsd { get; init; }

    [JsonPropertyName("planning_start_date")]
    public DateOnly? PlanningStartDate { get; init; }

    [JsonPropertyName("planning_end_date")]
    public DateOnly? PlanningEndDate { get; init; }

    [JsonPropertyName("train_schedule")]
    public IReadOnlyList<TrainDayDto> TrainSchedule { get; init; } = [];

    [JsonPropertyName("plant_recovery")]
    public IReadOnlyList<PlantDayDto> PlantRecovery { get; init; } = [];
}

public sealed class TrainDayDto
{
    [JsonPropertyName("train")]
    public string Train { get; init; } = string.Empty;

    [JsonPropertyName("plant")]
    public string Plant { get; init; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; init; }

    [JsonPropertyName("date")]
    public DateOnly? Date { get; init; }

    [JsonPropertyName("recovery")]
    public double Recovery { get; init; }

    [JsonPropertyName("changed")]
    public bool Changed { get; init; }
}

public sealed class PlantDayDto
{
    [JsonPropertyName("plant")]
    public string Plant { get; init; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; init; }

    [JsonPropertyName("date")]
    public DateOnly? Date { get; init; }

    [JsonPropertyName("recovery")]
    public double Recovery { get; init; }
}

public sealed class ValidationProblemDetailsDto
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]> Errors { get; init; } = [];
}
