namespace MPC.VCO.DST.Application.Exceptions;

public sealed class OptimizerInfeasibleException(string status)
    : Exception($"No feasible optimization solution was found. Solver status: {status}.")
{
    public string Status { get; } = status;
}
