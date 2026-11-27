using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Infrastructure.Configuration;
using MPC.VCO.DST.Infrastructure.Entities;
using MPC.VCO.DST.Infrastructure.Repositories;
using MPC.VCO.DST.Infrastructure.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using MPC.VCO.DST.Infrastructure.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace MPC.VCO.DST.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        CosmosDbOptions cosmosDbOptions =
            configuration
                .GetSection("CosmosDb")
                .Get<CosmosDbOptions>()
            ?? new CosmosDbOptions();

        string endpoint =
            cosmosDbOptions.Endpoint;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "CosmosDb:Endpoint is missing.");
        }

        string key =
            cosmosDbOptions.Key;

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "CosmosDb:Key is missing. Configure it through environment variables or a secure secret store.");
        }

        services.AddSingleton<IOptions<CosmosDbOptions>>(
            Options.Create(cosmosDbOptions));

        services.AddSingleton<CosmosClient>(_ =>
        {
            var options = new CosmosClientOptions
            {
                // Your corporate/local environment had
                // Direct-mode TCP connectivity problems.
                ConnectionMode = ConnectionMode.Gateway,

                UseSystemTextJsonSerializerWithOptions =
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }
            };

            return new CosmosClient(
                endpoint,
                key,
                options);
        });

        services.AddScoped<DstDbContext>();
        services.AddDbContext<dstSqlDbContext>(options =>
    options.UseSqlServer(SqlOptimizerOptions.BuildConnectionString(configuration)));

        services.AddScoped<IForecastRepository, ForecastRepository>();
        services.AddScoped<IPortfolioOptimizer, OrToolsPortfolioOptimizer>();

        services.AddScoped<
            IPlanningCycleRepository,
            PlanningCycleRepository>();

        services.AddScoped<
            IModelRepository,
            ModelRepository>();

        services.AddScoped<
            IModelNoteRepository,
            ModelNoteRepository>();

        services.AddHttpClient<IMentionUserDirectory, GraphMentionUserDirectory>();

        return services;
    }
}