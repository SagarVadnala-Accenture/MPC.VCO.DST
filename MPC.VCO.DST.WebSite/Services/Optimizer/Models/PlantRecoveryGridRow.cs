namespace MPC.VCO.DST.WebSite.Services.Optimizer.Models;

public sealed record PlantRecoveryGridRow(
    string Plant,
    IReadOnlyList<int> Days,
    IReadOnlyDictionary<int, double> Recoveries,
    double? AverageRecovery);
