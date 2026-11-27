using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Moq;

namespace MPC.VCO.DST.Tests.Infrastructure;

[TestClass]
public sealed class ServiceRegistrationTests
{
    [TestMethod]
    public void AddInfrastructure_NullServices_ThrowsArgumentNullException()
    {
        IConfiguration configuration = BuildValidConfiguration();

        ArgumentNullException exception = AssertThrows<ArgumentNullException>(
            () => ServiceRegistration.AddInfrastructure(null!, configuration));

        Assert.AreEqual("services", exception.ParamName);
    }

    [TestMethod]
    public void AddInfrastructure_NullConfiguration_ThrowsArgumentNullException()
    {
        ServiceCollection services = new();

        ArgumentNullException exception = AssertThrows<ArgumentNullException>(
            () => services.AddInfrastructure(null!));

        Assert.AreEqual("configuration", exception.ParamName);
    }

    [TestMethod]
    public void AddInfrastructure_MissingEndpoint_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM="
            })
            .Build();

        ServiceCollection services = new();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        StringAssert.Contains(exception.Message, "CosmosDb:Endpoint is missing");
    }

    [TestMethod]
    public void AddInfrastructure_WhitespaceEndpoint_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "   ",
                ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM="
            })
            .Build();

        ServiceCollection services = new();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        StringAssert.Contains(exception.Message, "CosmosDb:Endpoint is missing");
    }

    [TestMethod]
    public void AddInfrastructure_MissingKey_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://localhost:8081/"
            })
            .Build();

        ServiceCollection services = new();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        StringAssert.Contains(exception.Message, "CosmosDb:Key is missing");
    }

    [TestMethod]
    public void AddInfrastructure_WhitespaceKey_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                ["CosmosDb:Key"] = "  "
            })
            .Build();

        ServiceCollection services = new();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        StringAssert.Contains(exception.Message, "secure secret store");
    }

    [TestMethod]
    public void AddInfrastructure_ValidConfiguration_RegistersCoreServices()
    {
        IConfiguration configuration = BuildValidConfiguration();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(CosmosClient)));
        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(IModelRepository)));
        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(IPlanningCycleRepository)));
        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(IModelNoteRepository)));
        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(IMentionUserDirectory)));
    }

    [TestMethod]
    public void GraphMentionUserDirectory_MissingTokenAcquisitionDependency_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM="
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => provider.GetRequiredService<IMentionUserDirectory>());

        StringAssert.Contains(exception.Message, "ITokenAcquisition");
    }

    [TestMethod]
    public void GraphMentionUserDirectory_MissingScopes_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM=",
                ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new Mock<ITokenAcquisition>().Object);
        services.AddInfrastructure(configuration);

        ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => provider.GetRequiredService<IMentionUserDirectory>());

        StringAssert.Contains(exception.Message, "Graph:Scopes");
    }

    [TestMethod]
    public void GraphMentionUserDirectory_InvalidGroupId_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM=",
                ["Graph:Scopes:0"] = "User.Read",
                ["MentionUsers:AllowedGroupId"] = "not-a-guid"
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new Mock<ITokenAcquisition>().Object);
        services.AddInfrastructure(configuration);

        ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => provider.GetRequiredService<IMentionUserDirectory>());

        StringAssert.Contains(exception.Message, "must be a valid Entra group object ID");
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

    private static IConfiguration BuildValidConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM=",
                ["Graph:Scopes:0"] = "User.Read",
                ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
            })
            .Build();
}
