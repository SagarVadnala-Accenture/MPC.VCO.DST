namespace MPC.VCO.DST.Application.Exceptions;

public sealed class ForecastUnavailableException : Exception
{
    public ForecastUnavailableException(string message)
        : base(message)
    {
    }

    public ForecastUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
