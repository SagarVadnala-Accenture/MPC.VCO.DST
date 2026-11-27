using Microsoft.Extensions.Options;

namespace MPC.VCO.DST.WebSite.API.Configuration;

public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string? Instance { get; init; }

    public string? TenantId { get; init; }

    public string? Audience { get; init; }

    public string[]? Scopes { get; init; }
}

public static class AzureAdOptionsServiceCollectionExtensions
{
    public static IServiceCollection AddAzureAdOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AzureAdOptions>()
            .Bind(configuration.GetSection(AzureAdOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.TenantId),
                "AzureAd:TenantId is missing.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "AzureAd:Audience is missing.")
            .ValidateOnStart();

        return services;
    }
}