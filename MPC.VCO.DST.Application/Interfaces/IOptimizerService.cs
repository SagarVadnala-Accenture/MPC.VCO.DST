using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;

namespace MPC.VCO.DST.Application.Interfaces
{
    public interface IOptimizerService
    {
        Task<OptimizeResponse> OptimizeAsync(
            OptimizeRequest request,
            CancellationToken cancellationToken = default);
    }
}
