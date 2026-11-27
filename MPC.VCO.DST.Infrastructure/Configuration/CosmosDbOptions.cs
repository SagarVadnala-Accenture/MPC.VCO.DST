namespace MPC.VCO.DST.Infrastructure.Configuration;

internal sealed class CosmosDbOptions
{
    public string Endpoint { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string PlanningContainerName { get; init; } = string.Empty;

    public string ModelNotesContainerName { get; init; } = string.Empty;
}
