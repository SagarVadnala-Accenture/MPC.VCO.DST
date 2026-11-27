using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

namespace MPC.VCO.DST.WebSite.Configuration;

public static class AuthenticationServiceExtensions
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string[] scopes =
            configuration.GetSection("DstApi:Scopes").Get<string[]>()
            ?? throw new InvalidOperationException(
                "Configuration key 'DstApi:Scopes' is required.");

        if (scopes.Length == 0)
        {
            throw new InvalidOperationException(
                "At least one downstream API scope is required in 'DstApi:Scopes'.");
        }

        services
            .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(configuration.GetSection("AzureAd"))
            .EnableTokenAcquisitionToCallDownstreamApi(scopes)
            .AddInMemoryTokenCaches();

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());

        services.AddCascadingAuthenticationState();
        services.AddControllersWithViews().AddMicrosoftIdentityUI();

        return services;
    }
}
