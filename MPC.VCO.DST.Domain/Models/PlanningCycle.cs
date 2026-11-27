using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Domain.Models
{
    public class PlanningCycle
    {
        public string PlanningCycleId { get; init; } = string.Empty;

        public int Year { get; init; }

        public int Month { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateOnly StartDate { get; init; }

        public DateOnly EndDate { get; init; }

        public string CycleState { get; init; } = string.Empty;

        public BestModel? BestModel { get; init; }
    }
}
