using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MPC.VCO.DST.Tests.Helpers;
using MPC.VCO.DST.WebSite.API.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MPC.VCO.DST.Tests.API.Configuration;

[TestClass]
public sealed class StartupAndConfigurationTests
{
    [TestMethod]
    public void WebSite_AppSettingsJson_ContainsExpectedAzureAdDefaults()
    {
        string appSettingsPath = TestPathHelper.FindFile(
            Path.Combine("MPC.VCO.DST.WebSite", "appsettings.json"));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false)
            .Build();

        Assert.AreEqual("https://login.microsoftonline.com/", configuration["AzureAd:Instance"]);
        Assert.AreEqual("/signin-oidc", configuration["AzureAd:CallbackPath"]);
        Assert.AreEqual("/signout-callback-oidc", configuration["AzureAd:SignedOutCallbackPath"]);
        Assert.AreEqual("*", configuration["AllowedHosts"]);
    }

    [TestMethod]
    public void Api_AppSettingsJson_ContainsExpectedAzureAndGraphDefaults()
    {
        string appSettingsPath = TestPathHelper.FindFile(
            Path.Combine("MPC.VCO.DST.WebSite.API", "appsettings.json"));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false)
            .Build();

        Assert.AreEqual("https://login.microsoftonline.com/", configuration["AzureAd:Instance"]);
        Assert.AreEqual("https://graph.microsoft.com/User.ReadBasic.All", configuration["Graph:Scopes:0"]);
        Assert.AreEqual("https://graph.microsoft.com/GroupMember.ReadBasic.All", configuration["Graph:Scopes:1"]);
    }

    [TestMethod]
    public void WebAuthenticationServiceExtensions_MissingScopes_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "tenant-id",
                ["AzureAd:ClientId"] = "web-client-id"
            })
            .Build();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => MPC.VCO.DST.WebSite.Configuration.AuthenticationServiceExtensions
                .AddAuthenticationServices(services, configuration));

        StringAssert.Contains(exception.Message, "DstApi:Scopes");
    }

    [TestMethod]
    public void WebAuthenticationServiceExtensions_EmptyScopeValue_RegistersServices()
    {
        ServiceCollection services = new();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "tenant-id",
                ["AzureAd:ClientId"] = "web-client-id",
                ["DstApi:Scopes:0"] = string.Empty
            })
            .Build();

        MPC.VCO.DST.WebSite.Configuration.AuthenticationServiceExtensions
            .AddAuthenticationServices(services, configuration);

        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService)));
    }

    [TestMethod]
    public void OpenApiServiceExtensions_MissingTenant_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Audience"] = "api://test-client"
            })
            .Build();

        MPC.VCO.DST.WebSite.API.Configuration.AzureAdOptionsServiceCollectionExtensions
            .AddAzureAdOptions(services, configuration);
        MPC.VCO.DST.WebSite.API.Configuration.OpenApiServiceExtensions
            .AddOpenApiServices(services);

        ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = AssertThrows<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<MPC.VCO.DST.WebSite.API.Configuration.AzureAdOptions>>().Value);

        StringAssert.Contains(exception.Message, "AzureAd:TenantId is missing");
    }

    [TestMethod]
    public void OpenApiServiceExtensions_MissingAudience_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:TenantId"] = "tenant-id"
            })
            .Build();

        MPC.VCO.DST.WebSite.API.Configuration.AzureAdOptionsServiceCollectionExtensions
            .AddAzureAdOptions(services, configuration);
        MPC.VCO.DST.WebSite.API.Configuration.OpenApiServiceExtensions
            .AddOpenApiServices(services);

        ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = AssertThrows<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<MPC.VCO.DST.WebSite.API.Configuration.AzureAdOptions>>().Value);

        StringAssert.Contains(exception.Message, "AzureAd:Audience is missing");
    }

    [TestMethod]
    public void OpenApiServiceExtensions_ValidConfig_RegistersSwaggerWithOAuthScope()
    {
        ServiceCollection services = new();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com",
                ["AzureAd:TenantId"] = "tenant-id",
                ["AzureAd:Audience"] = "api://test-client",
                ["AzureAd:Scopes:0"] = "api://test-client/access_as_user"
            })
            .Build();

        MPC.VCO.DST.WebSite.API.Configuration.AzureAdOptionsServiceCollectionExtensions
            .AddAzureAdOptions(services, configuration);
        MPC.VCO.DST.WebSite.API.Configuration.OpenApiServiceExtensions
            .AddOpenApiServices(services);

        ServiceProvider provider = services.BuildServiceProvider();
        SwaggerGenOptions options = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;

        Assert.IsTrue(options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey("v1"));
        var scheme = options.SwaggerGeneratorOptions.SecuritySchemes["oauth2"];
        Assert.IsNotNull(scheme);
        Assert.IsNotNull(scheme.Flows);
        Assert.IsNotNull(scheme.Flows.AuthorizationCode);
        var authorizationCodeFlow = scheme.Flows.AuthorizationCode;
        if (authorizationCodeFlow is null)
        {
            Assert.Fail("Authorization code flow is null.");
            return;
        }

        IDictionary<string, string>? scopes = authorizationCodeFlow.Scopes;
        Assert.IsNotNull(scopes);
        Assert.IsTrue(scopes.ContainsKey("api://test-client/access_as_user"));
    }

    [TestMethod]
    public void WebAuthenticationApplicationBuilderExtensions_NullApp_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = AssertThrows<ArgumentNullException>(
            () => MPC.VCO.DST.WebSite.Configuration.AuthenticationApplicationBuilderExtensions
                .UseAuthenticationServices(null!));

        Assert.AreEqual("app", exception.ParamName);
    }

    [TestMethod]
    public void WebAuthenticationApplicationBuilderExtensions_ValidApp_ReturnsSameInstance()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAuthorization();
        services.AddAuthentication("TestScheme");

        ApplicationBuilder app = new(services.BuildServiceProvider());

        IApplicationBuilder result = MPC.VCO.DST.WebSite.Configuration
            .AuthenticationApplicationBuilderExtensions
            .UseAuthenticationServices(app);

        Assert.AreSame(app, result);
    }

    private static TException AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }
        catch (TException exception)
        {
            return exception;
        }
    }
}
