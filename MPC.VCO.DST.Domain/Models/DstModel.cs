using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Domain.Models
{
    public class DstModel
    {
        public string ModelId { get; init; } = string.Empty;

        public string PlanningCycleId { get; init; } = string.Empty;

        public DateOnly CreatedOn { get; init; }

        public string Status { get; init; } = string.Empty;

        public ModelScope Scope { get; init; } = new();

        public string ModelType { get; init; } = string.Empty;

        public decimal EthanePriceUsdPerGal { get; init; }

        public decimal NaturalGasPriceUsdPerMmbtu { get; init; }

        //To enable recovery percent calculation, uncomment the following line

        //public decimal AverageRecoveryPercent { get; init; }

        public decimal ExpectedValueMonthlyUsd { get; init; }

    }
}
