using MPC.VCO.DST.Application.DTOs.Request;
using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class OptimizeRequest
{
    [JsonPropertyName("planning_start_date")]
    public DateOnly? PlanningStartDate { get; init; }

    [JsonPropertyName("planning_end_date")]
    public DateOnly? PlanningEndDate { get; init; }

    [JsonPropertyName("horizon_days")]
    public int HorizonDays { get; init; } = 30;

    [JsonPropertyName("ethane_price_shift_pct")]
    public double EthanePriceShiftPct { get; init; }

    [JsonPropertyName("gas_price_shift_pct")]
    public double GasPriceShiftPct { get; init; }

    [JsonPropertyName("tf_fee")]
    public double TfFee { get; init; } = 4.0;

    [JsonPropertyName("ethane_heating_value")]
    public double EthaneHeatingValue { get; init; } = 2.8;

    [JsonPropertyName("takeaway_bbl_per_day")]
    public double? TakeawayBblPerDay { get; init; }

    [JsonPropertyName("k_fix")]
    public double KFix { get; init; } = 5000.0;

    [JsonPropertyName("k_var")]
    public double KVar { get; init; } = 200000.0;

    [JsonPropertyName("max_changes_per_train")]
    public int MaxChangesPerTrain { get; init; } = 4;

    [JsonPropertyName("min_hold_days")]
    public int MinHoldDays { get; init; } = 1;

    [JsonPropertyName("time_limit_sec")]
    public int TimeLimitSec { get; init; } = 120;

    [JsonPropertyName("mip_gap")]
    public double MipGap { get; init; } = 0.01;

    [JsonPropertyName("trains")]
    public Dictionary<string, TrainSettings>? Trains { get; init; } = [];

    public (DateOnly StartDate, DateOnly EndDate) ResolvePlanningDates(DateOnly todayUtc) =>
        PlanningStartDate is DateOnly start && PlanningEndDate is DateOnly end
            ? (start, end)
            : (todayUtc, todayUtc.AddDays(30));
}
