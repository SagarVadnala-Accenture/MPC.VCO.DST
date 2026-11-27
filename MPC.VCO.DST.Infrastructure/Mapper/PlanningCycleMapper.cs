using MPC.VCO.DST.Domain.Models;
using MPC.VCO.DST.Infrastructure.Entities;

namespace MPC.VCO.DST.Infrastructure.Mapper;

internal static class PlanningCycleMapper
{
    public static PlanningCycle ToDomain(
        this PlanningCycleEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PlanningCycle
        {
            PlanningCycleId =
                entity.PlanningCycleId,

            Year =
                entity.Year,

            Month =
                entity.Month,

            Name =
                entity.Name,

            StartDate =
                entity.StartDate,

            EndDate =
                entity.EndDate,

            CycleState =
                entity.CycleState,

            BestModel =
                entity.BestModel is null
                    ? null
                    : new BestModel
                    {
                        ModelId =
                            entity.BestModel.ModelId,

                        //AverageRecoveryPercent =
                        //    entity.BestModel
                        //        .AverageRecoveryPercent,

                        ExpectedValueMonthlyUsd =
                            entity.BestModel
                                .ExpectedValueMonthlyUsd
                    }
        };
    }
}