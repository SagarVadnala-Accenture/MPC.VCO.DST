using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Infrastructure.Entities
{
    internal sealed class ModelEntity
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; } = string.Empty;

        [JsonPropertyName("planningCycleId")]
        public string PlanningCycleId { get; set; } = string.Empty;

        [JsonPropertyName("modelId")]
        public string ModelId { get; set; } = string.Empty;

        [JsonPropertyName("createdOn")]
        public string CreatedOn { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("scope")]
        public ModelScopeEntity Scope { get; set; } = new();

        [JsonPropertyName("modelType")]
        public string ModelType { get; set; } = string.Empty;

        [JsonPropertyName("ethanePriceUsdPerGal")]
        public decimal EthanePriceUsdPerGal { get; set; }

        [JsonPropertyName("naturalGasPriceUsdPerMmbtu")]
        public decimal NaturalGasPriceUsdPerMmbtu { get; set; }
        
    //To enable recovery percent calculation, uncomment the following line

        //[JsonPropertyName("averageRecoveryPercent")]
        //public decimal AverageRecoveryPercent { get; set; }

        [JsonPropertyName("expectedValueMonthlyUsd")]
        public decimal ExpectedValueMonthlyUsd { get; set; }
    }
}
