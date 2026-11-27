using MPC.VCO.DST.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Domain.Interfaces
{
    public interface IModelRepository
    {
        Task<string>
            ReserveNextModelIdAsync(
                string planningCycleId,
                CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DstModel>>
        GetByPlanningCycleAsync(
            string planningCycleId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DstModel>>
        GetByPlanningCycleAsync(
            string planningCycleId,
            CancellationToken cancellationToken = default);

        Task<DstModel?>
            GetByIdAsync(
                string planningCycleId,
                string modelId,
                CancellationToken cancellationToken = default);

        Task<DstModel?>
            GetHighestExpectedValueModelAsync(
                string planningCycleId,
                CancellationToken cancellationToken = default);

        Task<DstModel>
            CreateAsync(
                DstModel model,
                CancellationToken cancellationToken = default);

        Task<DstModel?>
            UpdateAsync(
                DstModel model,
                CancellationToken cancellationToken = default);

        Task<bool>
            DeleteAsync(
                string planningCycleId,
                string modelId,
                CancellationToken cancellationToken = default);
    }
}
