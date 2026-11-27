namespace MPC.VCO.DST.WebSite.Configuration;

public static class AuthenticationApplicationBuilderExtensions
{
    public static IApplicationBuilder UseAuthenticationServices(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
