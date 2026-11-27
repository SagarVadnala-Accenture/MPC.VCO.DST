using MPC.VCO.DST.WebSite.Services;
using MPC.VCO.DST.WebSite.Services.Optimizer;

namespace MPC.VCO.DST.WebSite.Configuration;

public static class DstApiHttpClientExtensions
{
    public static IServiceCollection AddDstApiClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string baseUrl =
            configuration["DstApi:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Configuration key 'DstApi:BaseUrl' is required.");

        services.AddScoped<DstApiAccessTokenHandler>();
        services.AddScoped<DstApiAuthenticatedRequestSender>();

        services.AddHttpClient<PlanningCyclesApiClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddHttpMessageHandler<DstApiAccessTokenHandler>();

        services.AddHttpClient<ModelsApiClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddHttpMessageHandler<DstApiAccessTokenHandler>();

        services.AddHttpClient<IOptimizerApiClient, OptimizerApiClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(230);
        })
     .AddHttpMessageHandler<DstApiAccessTokenHandler>();
        services.AddScoped<OptimizerScenarioStateStore>();

        return services;
    }
}
