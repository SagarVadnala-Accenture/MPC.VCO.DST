using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Domain.Models.Optimizer;
using MPC.VCO.DST.Application.DTOs.Request;


namespace MPC.VCO.DST.Application.Interfaces;

    public interface IPortfolioOptimizer
{
    Task<PortfolioOptimizationResult> SolveAsync(
        OptimizeRequest request,
        Forecast forecast,
        CancellationToken cancellationToken = default);
}

public sealed record PortfolioOptimizationResult(
    string Status,
    double? MarginUsd,
    IReadOnlyList<TrainDay> TrainSchedule,
    IReadOnlyList<PlantDay> PlantRecovery);
