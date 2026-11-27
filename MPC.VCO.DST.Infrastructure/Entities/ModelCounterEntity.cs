using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Infrastructure.Entities;

internal sealed class ModelCounterEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("planningCycleId")]
    public string PlanningCycleId { get; set; } = string.Empty;

    [JsonPropertyName("lastAllocatedSequence")]
    public int LastAllocatedSequence { get; set; }
}