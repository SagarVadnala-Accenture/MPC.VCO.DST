using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Mappers;
using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Domain.Models;

namespace MPC.VCO.DST.Application.Implementation;

internal sealed class ModelService(
    IModelRepository modelRepository)
    : IModelService
{
    private static readonly HashSet<string> AllowedStatuses =
        ["Draft", "Reviewed", "Executable"];

    private readonly IModelRepository _modelRepository =
        modelRepository;

    public async Task<IReadOnlyList<ModelResponse>>
        GetModelsAsync(
            GetModelsRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(
                request.PlanningCycleId))
        {
            throw new ArgumentException(
                "PlanningCycleId is required.",
                nameof(request));
        }

        if (request.PageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PageNumber));
        }

        if (request.PageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PageSize));
        }

        var models =
            await _modelRepository
                .GetByPlanningCycleAsync(
                    request.PlanningCycleId,
                    request.PageNumber,
                    request.PageSize,
                    cancellationToken);

        return models
            .Select(x => x.ToResponse())
            .ToList();
    }

    public async Task<ModelResponse?>
        GetModelAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                planningCycleId))
        {
            throw new ArgumentException(
                "PlanningCycleId is required.",
                nameof(planningCycleId));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException(
                "ModelId is required.",
                nameof(modelId));
        }

        var model =
            await _modelRepository.GetByIdAsync(
                planningCycleId,
                modelId,
                cancellationToken);

        return model?.ToResponse();
    }

    public async Task<ModelResponse>
        CreateModelAsync(
            CreateModelRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PlanningCycleId))
        {
            throw new ArgumentException(
                "PlanningCycleId is required.",
                nameof(request.PlanningCycleId));
        }

        ValidateModelInput(
            request.Status,
            request.Scope,
            request.ModelType,
            request.EthanePriceUsdPerGal,
            request.NaturalGasPriceUsdPerMmbtu);

        string modelId =
            await _modelRepository.ReserveNextModelIdAsync(
                request.PlanningCycleId,
                cancellationToken);

        DstModel model = new()
        {
            ModelId = modelId,
            PlanningCycleId = request.PlanningCycleId,  
            CreatedOn = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime),
            Status = request.Status.Trim(),
            Scope = new()
            {
                Type = request.Scope.Type.Trim(),
                Name = request.Scope.Name.Trim()
            },
            ModelType = request.ModelType.Trim(),
            EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
            NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu,

            //To enable recovery percent calculation, uncomment the following line
            //AverageRecoveryPercent = 0,
            ExpectedValueMonthlyUsd = 0
        };

        DstModel created =
            await _modelRepository.CreateAsync(
                model,
                cancellationToken);

        return created.ToResponse();
    }

    public async Task<ModelResponse?>
        UpdateModelAsync(
            UpdateModelRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PlanningCycleId))
        {
            throw new ArgumentException(
                "PlanningCycleId is required.",
                nameof(request.PlanningCycleId));
        }

        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new ArgumentException(
                "ModelId is required.",
                nameof(request));
        }

        ValidateModelInput(
            request.Status,
            request.Scope,
            request.ModelType,
            request.EthanePriceUsdPerGal,
            request.NaturalGasPriceUsdPerMmbtu);

        DstModel? existing =
            await _modelRepository.GetByIdAsync(
                request.PlanningCycleId,
                request.ModelId,
                cancellationToken);

        if (existing is null)
        {
            return null;
        }

        DstModel updatedModel = new()
        {
            ModelId = existing.ModelId,
            PlanningCycleId = existing.PlanningCycleId,
            CreatedOn = existing.CreatedOn,
            Status = request.Status.Trim(),
            Scope = new()
            {
                Type = request.Scope.Type.Trim(),
                Name = request.Scope.Name.Trim()
            },
            ModelType = request.ModelType.Trim(),
            EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
            NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu,
            
            //To enable recovery percent calculation, uncomment the following line
            //AverageRecoveryPercent = existing.AverageRecoveryPercent,
            ExpectedValueMonthlyUsd = existing.ExpectedValueMonthlyUsd
        };

        DstModel? updated =
            await _modelRepository.UpdateAsync(
                updatedModel,
                cancellationToken);

        return updated?.ToResponse();
    }

    public Task<bool>
        DeleteModelAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planningCycleId))
        {
            throw new ArgumentException(
                "PlanningCycleId is required.",
                nameof(planningCycleId));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException(
                "ModelId is required.",
                nameof(modelId));
        }

        return _modelRepository.DeleteAsync(
            planningCycleId,
            modelId,
            cancellationToken);
    }

    private static void ValidateModelInput(
        string status,
        ModelScopeRequest scope,
        string modelType,
        decimal ethanePriceUsdPerGal,
        decimal naturalGasPriceUsdPerMmbtu)
    {
        if (string.IsNullOrWhiteSpace(status)
            || !AllowedStatuses.Contains(status.Trim()))
        {
            throw new ArgumentException(
                "Status must be one of: Draft, Reviewed, Executable.",
                nameof(status));
        }

        if (scope is null)
        {
            throw new ArgumentException(
                "Scope is required.",
                nameof(scope));
        }

        if (string.IsNullOrWhiteSpace(scope.Type)
            || string.IsNullOrWhiteSpace(scope.Name))
        {
            throw new ArgumentException(
                "Scope.Type and Scope.Name are required.",
                nameof(scope));
        }

        if (string.IsNullOrWhiteSpace(modelType))
        {
            throw new ArgumentException(
                "ModelType is required.",
                nameof(modelType));
        }

        if (ethanePriceUsdPerGal <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ethanePriceUsdPerGal),
                "Ethane price must be greater than zero.");
        }

        if (naturalGasPriceUsdPerMmbtu <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(naturalGasPriceUsdPerMmbtu),
                "Natural gas price must be greater than zero.");
        }
    }

}