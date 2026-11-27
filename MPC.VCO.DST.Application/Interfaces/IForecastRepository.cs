using MPC.VCO.DST.Domain.Models.Optimizer;

namespace MPC.VCO.DST.Application.Interfaces
{
    public interface IForecastRepository
    {
        Task<Forecast> GetForecastAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default);
    }

}
