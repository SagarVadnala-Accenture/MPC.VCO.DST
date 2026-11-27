using MPC.VCO.DST.Domain.Models;
using MPC.VCO.DST.Infrastructure.Entities;


namespace MPC.VCO.DST.Infrastructure.Mapper;

internal static class ModelMapper
{
    public static DstModel ToDomain(
        this ModelEntity entity)
    {
        return new DstModel
        {
            ModelId = entity.ModelId,

            PlanningCycleId =
                entity.PlanningCycleId,

            CreatedOn =
                DateOnly.Parse(entity.CreatedOn),

            Status = entity.Status,

            Scope = new ModelScope
            {
                Type = entity.Scope.Type,
                Name = entity.Scope.Name
            },

            ModelType = entity.ModelType,

            EthanePriceUsdPerGal =
                entity.EthanePriceUsdPerGal,

            NaturalGasPriceUsdPerMmbtu =
                entity.NaturalGasPriceUsdPerMmbtu,

            //AverageRecoveryPercent =
            //    entity.AverageRecoveryPercent,

            ExpectedValueMonthlyUsd =
                entity.ExpectedValueMonthlyUsd
        };
    }

    public static ModelEntity ToEntity(
        this DstModel model,
        string id)
    {
        return new ModelEntity
        {
            Id = id,
            DocumentType = "model",
            PlanningCycleId = model.PlanningCycleId,
            ModelId = model.ModelId,
            CreatedOn = model.CreatedOn.ToString("yyyy-MM-dd"),
            Status = model.Status,
            Scope = new ModelScopeEntity
            {
                Type = model.Scope.Type,
                Name = model.Scope.Name
            },
            ModelType = model.ModelType,
            EthanePriceUsdPerGal = model.EthanePriceUsdPerGal,
            NaturalGasPriceUsdPerMmbtu = model.NaturalGasPriceUsdPerMmbtu,
            //AverageRecoveryPercent = model.AverageRecoveryPercent,
            ExpectedValueMonthlyUsd = model.ExpectedValueMonthlyUsd
        };
    }
}
