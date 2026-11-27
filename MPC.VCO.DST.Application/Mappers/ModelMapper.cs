using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Domain.Models;

namespace MPC.VCO.DST.Application.Mappers;

internal static class ModelMapper
{
    public static ModelResponse ToResponse(
        this DstModel model)
    {
        return new ModelResponse
        {
            ModelId =
                model.ModelId,

            CreatedOn =
                model.CreatedOn,

            Status =
                model.Status,

            Scope =
                new ModelScopeResponse
                {
                    Type =
                        model.Scope.Type,

                    Name =
                        model.Scope.Name
                },

            ModelType =
                model.ModelType,

            EthanePriceUsdPerGal =
                model.EthanePriceUsdPerGal,

            NaturalGasPriceUsdPerMmbtu =
                model.NaturalGasPriceUsdPerMmbtu,

            //To enable recovery percent calculation, uncomment the following line
            //AverageRecoveryPercent = model.AverageRecoveryPercent,

            ExpectedValueMonthlyUsd =
                model.ExpectedValueMonthlyUsd
        };
    }
}