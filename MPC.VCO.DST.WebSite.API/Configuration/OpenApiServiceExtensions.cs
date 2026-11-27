using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MPC.VCO.DST.WebSite.API.Configuration;

public static class OpenApiServiceExtensions
{
    public static IServiceCollection AddOpenApiServices(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSwaggerGen();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureOpenApiSwaggerGenOptions>();

        return services;
    }

    public static WebApplication UseOpenApiServices(
        this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        string? swaggerScope =
            app.Configuration["AzureAd:Scopes:0"];

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(
                "/swagger/v1/swagger.json",
                "MPC VCO DST API v1");

            string? clientId =
                app.Configuration["Swagger:ClientId"];

            if (!string.IsNullOrWhiteSpace(clientId))
            {
                options.OAuthClientId(clientId);
                options.OAuthUsePkce();

                if (!string.IsNullOrWhiteSpace(swaggerScope))
                {
                    options.OAuthScopes(swaggerScope);
                }
            }
        });

        return app;
    }
}

internal sealed class ConfigureOpenApiSwaggerGenOptions(
    IOptions<AzureAdOptions> azureAdOptions)
    : IConfigureOptions<SwaggerGenOptions>
{
    private readonly AzureAdOptions _azureAd = azureAdOptions.Value;

    public void Configure(SwaggerGenOptions options)
    {
        string instance = _azureAd.Instance ?? "https://login.microsoftonline.com/";
        if (!instance.EndsWith("/", StringComparison.Ordinal))
        {
            instance += "/";
        }

        string tenantId = _azureAd.TenantId!;
        string audience = _azureAd.Audience!;

        string scope =
            _azureAd.Scopes?.FirstOrDefault(scopeValue => !string.IsNullOrWhiteSpace(scopeValue))
            ?? $"{audience}/access_as_user";

        string authority = $"{instance}{tenantId}/oauth2/v2.0";

        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "MPC VCO DST API",
                Version = "v1"
            });

        options.AddSecurityDefinition(
            "oauth2",
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{authority}/authorize"),
                        TokenUrl = new Uri($"{authority}/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            [scope] = "Access DST protected API"
                        }
                    }
                }
            });

        options.AddSecurityRequirement(document =>
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecuritySchemeReference(
                        "oauth2",
                        document)
                ] = [scope]
            });
    }
}