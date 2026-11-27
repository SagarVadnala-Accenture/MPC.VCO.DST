using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Infrastructure.Entities
{
    internal sealed class PlanningCycleEntity
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; } = string.Empty;

        [JsonPropertyName("planningCycleId")]
        public string PlanningCycleId { get; set; } = string.Empty;

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("month")]
        public int Month { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public DateOnly StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateOnly EndDate { get; set; }

        [JsonPropertyName("cycleState")]
        public string CycleState { get; set; } = string.Empty;

        [JsonPropertyName("bestModel")]
        public BestModelEntity? BestModel { get; set; }
    }
}
