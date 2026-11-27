namespace MPC.VCO.DST.WebSite.Models;

public enum ModelStatus { Draft, Reviewed, Executable }

public sealed record PlanningCycle(
    string PlanningCycleId,
    DateOnly Period,
    int ModelCount,
    string? BestModelId,
    int? Recovery,
    int? ExpectedValue)
{
    public string Title => Period.ToString("MMMM, yyyy", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record ModelRun(
    string Id, DateOnly Period, DateOnly CreatedOn, ModelStatus Status,
    string Scope, string Location, string ModelType, decimal EthanePrice,
    decimal NaturalGasPrice, int? Recovery, int? ExpectedValue,
    string PlanningCycleId)
{
    public string ExpectedValueText => ExpectedValue is int value ? $"+${value}K/mo" : "-";
    public string RecoveryText => Recovery is int value ? $"{value}%" : "-";
}

public sealed record SortRequest(string Column, bool Additive);
