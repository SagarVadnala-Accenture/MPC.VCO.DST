using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Infrastructure.Entities
{
    internal sealed class BestModelEntity
    {
        [JsonPropertyName("modelId")]
        public string ModelId { get; set; } = string.Empty;

        
    //To enable recovery percent calculation, uncomment the following line
        //[JsonPropertyName("averageRecoveryPercent")]
        //public decimal AverageRecoveryPercent { get; set; }

        [JsonPropertyName("expectedValueMonthlyUsd")]
        public decimal ExpectedValueMonthlyUsd { get; set; }
    }
}
