namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class UpdateModelRequest
{
    public string PlanningCycleId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public ModelScopeRequest Scope { get; init; } = new();

    public string ModelType { get; init; } = string.Empty;

    public decimal EthanePriceUsdPerGal { get; init; }

    public decimal NaturalGasPriceUsdPerMmbtu { get; init; }
}