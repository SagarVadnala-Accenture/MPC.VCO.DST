using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;

namespace MPC.VCO.DST.Application.Interfaces;

public interface IModelService
{
    Task<IReadOnlyList<ModelResponse>>
        GetModelsAsync(
            GetModelsRequest request,
            CancellationToken cancellationToken = default);

    Task<ModelResponse?>
        GetModelAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default);

    Task<ModelResponse>
        CreateModelAsync(
            CreateModelRequest request,
            CancellationToken cancellationToken = default);

    Task<ModelResponse?>
        UpdateModelAsync(
            UpdateModelRequest request,
            CancellationToken cancellationToken = default);

    Task<bool>
        DeleteModelAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default);
}