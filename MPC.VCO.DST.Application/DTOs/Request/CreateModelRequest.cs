namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class CreateModelRequest
{
    public string PlanningCycleId { get; init; } = string.Empty;

    public string Status { get; init; } = "Draft";

    public ModelScopeRequest Scope { get; init; } = new();

    public string ModelType { get; init; } = string.Empty;

    public decimal EthanePriceUsdPerGal { get; init; }

    public decimal NaturalGasPriceUsdPerMmbtu { get; init; }
}