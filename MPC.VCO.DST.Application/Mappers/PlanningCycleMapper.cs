using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Domain.Models;

namespace MPC.VCO.DST.Application.Mappers;

internal static class PlanningCycleMapper
{
    public static PlanningCycleResponse ToResponse(
        this PlanningCycle model)
    {
        return new PlanningCycleResponse
        {
            PlanningCycleId = model.PlanningCycleId,
            Year = model.Year,
            Month = model.Month,
            Name = model.Name,
            CycleState = model.CycleState,
            BestModel = model.BestModel is null
                ? null
                : new BestModelResponse
                {
                    ModelId = model.BestModel.ModelId,

                    //To enable recovery percent calculation, uncomment the following line
                    //AverageRecoveryPercent = model.BestModel.AverageRecoveryPercent,

                    ExpectedValueMonthlyUsd = model.BestModel.ExpectedValueMonthlyUsd
                }
        };
    }
}