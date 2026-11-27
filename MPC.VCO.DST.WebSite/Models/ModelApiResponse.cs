namespace MPC.VCO.DST.WebSite.Models;

public sealed class ModelApiResponse
{
    public string ModelId { get; init; } = string.Empty;

    public DateOnly CreatedOn { get; init; }

    public string Status { get; init; } = string.Empty;

    public ModelScopeApiResponse Scope { get; init; } = new();

    public string ModelType { get; init; } = string.Empty;

    public decimal EthanePriceUsdPerGal { get; init; }

    public decimal NaturalGasPriceUsdPerMmbtu { get; init; }

    public decimal AverageRecoveryPercent { get; init; }

    public decimal ExpectedValueMonthlyUsd { get; init; }
}

public sealed class ModelScopeApiResponse
{
    public string Type { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
