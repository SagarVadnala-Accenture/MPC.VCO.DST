using MPC.VCO.DST.WebSite.Services.Optimizer.Models;

namespace MPC.VCO.DST.WebSite.Services.Optimizer;

public interface IOptimizerApiClient
{
    

   

    Task<OptimizeResponseDto> OptimizeAsync(
        OptimizeRequestDto request,
        CancellationToken cancellationToken = default);
}
