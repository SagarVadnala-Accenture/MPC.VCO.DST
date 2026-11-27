namespace MPC.VCO.DST.WebSite.Models;

public sealed class ModelUpsertRequest
{
    public string Status { get; set; } = "Draft";

    public string ScopeType { get; set; } = string.Empty;

    public string ScopeName { get; set; } = string.Empty;

    public string ModelType { get; set; } = string.Empty;

    public decimal EthanePriceUsdPerGal { get; set; }

    public decimal NaturalGasPriceUsdPerMmbtu { get; set; }
}
