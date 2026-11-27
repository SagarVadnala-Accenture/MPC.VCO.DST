using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Infrastructure.Entities
{
    internal sealed class ModelScopeEntity
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
