using MPC.VCO.DST.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Domain.Interfaces
{
    public interface IPlanningCycleRepository
    {
        Task<IReadOnlyList<PlanningCycle>>
       GetPlanningCyclesAsync(
           int year,
           int month,
           CancellationToken cancellationToken = default);

        Task<PlanningCycle?>
            GetPlanningCycleAsync(
                string planningCycleId,
                CancellationToken cancellationToken = default);
    }
}
