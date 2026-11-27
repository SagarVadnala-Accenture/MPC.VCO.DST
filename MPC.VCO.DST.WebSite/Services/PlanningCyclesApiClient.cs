using System.Net.Http.Json;
using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services;

public sealed class PlanningCyclesApiClient(
    HttpClient httpClient,
    DstApiAuthenticatedRequestSender requestSender,
    ILogger<PlanningCyclesApiClient> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly DstApiAuthenticatedRequestSender _requestSender = requestSender;
    private readonly ILogger<PlanningCyclesApiClient> _logger = logger;

    public async Task<PlanningCycle> GetPlanningCycleAsync(
        DateOnly period,
        CancellationToken cancellationToken = default)
    {
        if (period.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                "Month must be between 1 and 12.");
        }

        try
        {
            using HttpResponseMessage response =
                await _requestSender.SendAsync(
                    _httpClient,
                    HttpMethod.Get,
                    $"api/planning-cycles?year={period.Year}&month={period.Month}",
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            List<PlanningCycleApiResponse> planningCycles =
                await response.Content.ReadFromJsonAsync<
                    List<PlanningCycleApiResponse>>(
                    cancellationToken: cancellationToken)
                ?? [];

            return PlanningCycleMapper.Map(period, planningCycles);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogError(
                exception,
                "Failed to get planning cycle for {Year}-{Month}.",
                period.Year,
                period.Month);

            throw;
        }
    }
}
