using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Moq;
using MPC.VCO.DST.WebSite.Configuration;
using MPC.VCO.DST.WebSite.Services;
using MPC.VCO.DST.WebSite.Services.Optimizer;

namespace MPC.VCO.DST.Tests.UI.Services;

[TestClass]
public sealed class DstApiHttpClientExtensionsTests
{
    [TestMethod]
    public void AddDstApiClients_MissingDstApiBaseUrl_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OptimizerApi:BaseUrl"] = "https://optimizer.local/"
            })
            .Build();

        _ = AssertThrows<InvalidOperationException>(
            () => services.AddDstApiClients(configuration));
    }

    [TestMethod]
    public void AddDstApiClients_DoesNotRequireStandaloneOptimizerUrl()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DstApi:BaseUrl"] = "https://api.local/"
            })
            .Build();

        services.AddDstApiClients(configuration);

        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(IOptimizerApiClient)));
    }

    [TestMethod]
    public void AddDstApiClients_ValidConfiguration_ResolvesClientsAndUsesConfiguredBaseAddresses()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DstApi:BaseUrl"] = "https://api.local/",
                ["DstApi:Scopes:0"] = "api://client/access_as_user"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddSingleton<ITokenAcquisition>(new Mock<ITokenAcquisition>().Object);
        services.AddSingleton<AuthenticationStateProvider>(new StubAuthenticationStateProvider());
        services.AddSingleton<NavigationManager, StubNavigationManager>();

        services.AddDstApiClients(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider scopedProvider = scope.ServiceProvider;

        _ = scopedProvider.GetRequiredService<DstApiAccessTokenHandler>();
        _ = scopedProvider.GetRequiredService<DstApiAuthenticatedRequestSender>();
        _ = scopedProvider.GetRequiredService<PlanningCyclesApiClient>();
        _ = scopedProvider.GetRequiredService<ModelsApiClient>();
        _ = scopedProvider.GetRequiredService<IOptimizerApiClient>();
        _ = scopedProvider.GetRequiredService<OptimizerScenarioStateStore>();

        IHttpClientFactory httpClientFactory = scopedProvider.GetRequiredService<IHttpClientFactory>();

        HttpClient planningCyclesHttpClient = httpClientFactory.CreateClient(nameof(PlanningCyclesApiClient));
        HttpClient modelsHttpClient = httpClientFactory.CreateClient(nameof(ModelsApiClient));
        HttpClient optimizerHttpClient = httpClientFactory.CreateClient(nameof(IOptimizerApiClient));

        Assert.AreEqual(new Uri("https://api.local/"), planningCyclesHttpClient.BaseAddress);
        Assert.AreEqual(new Uri("https://api.local/"), modelsHttpClient.BaseAddress);
        Assert.AreEqual(new Uri("https://api.local/"), optimizerHttpClient.BaseAddress);
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

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly ClaimsPrincipal User = new(
            new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("name", "Test User")
            ], "TestAuth"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(User));
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager()
        {
            Initialize("https://app.local/", "https://app.local/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}

