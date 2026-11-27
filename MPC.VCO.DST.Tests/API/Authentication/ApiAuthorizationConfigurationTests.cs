using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MPC.VCO.DST.WebSite.API.Configuration;
using MPC.VCO.DST.WebSite.API.Controllers;

namespace MPC.VCO.DST.Tests.API.Authentication;

[TestClass]
public sealed class ApiAuthorizationConfigurationTests
{
    [TestMethod]
    public void AddAuthenticationServices_ConfiguresFallbackPolicyToRequireAuthenticatedUser()
    {
        var services = new ServiceCollection();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                    ["AzureAd:TenantId"] = "tenant-id",
                    ["AzureAd:ClientId"] = "api-client-id",
                    ["AzureAd:Audience"] = "api://api-client-id"
                })
            .Build();

        services.AddLogging();

        services.AddAuthenticationServices(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        AuthorizationOptions options =
            provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.IsNotNull(options.FallbackPolicy);
        Assert.IsTrue(
            options.FallbackPolicy.Requirements
                .OfType<DenyAnonymousAuthorizationRequirement>()
                .Any());
    }

    [TestMethod]
    public void ApiControllers_HaveAuthorizeAttribute()
    {
        Type[] controllerTypes =
        [
            typeof(PlanningCyclesController),
            typeof(ModelsController),
            typeof(ModelNotesController),
            typeof(OptimizerController)
        ];

        foreach (Type controllerType in controllerTypes)
        {
            Assert.IsTrue(typeof(ControllerBase).IsAssignableFrom(controllerType));

            bool hasAuthorize =
                controllerType
                    .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                    .Any();

            Assert.IsTrue(
                hasAuthorize,
                $"Controller '{controllerType.Name}' must be decorated with [Authorize].");
        }
    }
}
