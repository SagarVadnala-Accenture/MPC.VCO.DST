namespace MPC.VCO.DST.Application.Exceptions;

public sealed class OptimizerValidationException(
    IReadOnlyDictionary<string, string[]> errors)
    : Exception("The optimizer request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
