using System.Net;
using System.Text;
using MPC.VCO.DST.Application.Models;
using MPC.VCO.DST.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using Moq;

namespace MPC.VCO.DST.Tests.Infrastructure.Services;

[TestClass]
public sealed class GraphMentionUserDirectoryTests
{
    [TestMethod]
    public void Constructor_MissingScopes_ThrowsInvalidOperationException()
    {
        HttpClient httpClient = new(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Mock<ITokenAcquisition> tokenAcquisition = new();
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
        });

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => _ = CreateDirectory(httpClient, tokenAcquisition.Object, configuration));

        StringAssert.Contains(exception.Message, "Graph:Scopes");
    }

    [TestMethod]
    public void Constructor_MissingGroupId_ThrowsInvalidOperationException()
    {
        HttpClient httpClient = new(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Mock<ITokenAcquisition> tokenAcquisition = new();
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Graph:Scopes:0"] = "User.Read"
        });

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => _ = CreateDirectory(httpClient, tokenAcquisition.Object, configuration));

        StringAssert.Contains(exception.Message, "MentionUsers:AllowedGroupId");
    }

    [TestMethod]
    public void Constructor_InvalidGroupId_ThrowsInvalidOperationException()
    {
        HttpClient httpClient = new(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Mock<ITokenAcquisition> tokenAcquisition = new();
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Graph:Scopes:0"] = "User.Read",
            ["MentionUsers:AllowedGroupId"] = "not-a-guid"
        });

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => _ = CreateDirectory(httpClient, tokenAcquisition.Object, configuration));

        StringAssert.Contains(exception.Message, "valid Entra group object ID");
    }

    [TestMethod]
    public async Task SearchAsync_MapsUsersAndAddsBearerAuthorization()
    {
        HttpRequestMessage? capturedRequest = null;
        HttpClient httpClient = new(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            const string json = """
                {
                  "value": [
                    { "id": "u1", "displayName": "User One", "mail": "u1@contoso.com", "userPrincipalName": "u1@contoso.com" },
                    { "id": "u2", "displayName": "User Two", "mail": "", "userPrincipalName": "u2@contoso.com" },
                    { "id": "", "displayName": "Invalid", "mail": "invalid@contoso.com", "userPrincipalName": "invalid@contoso.com" }
                  ]
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }));

        Mock<ITokenAcquisition> tokenAcquisition = new();
        tokenAcquisition
            .Setup(acquisition => acquisition.GetAccessTokenForUserAsync(
                It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync("token-123");

        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Graph:Scopes:0"] = "User.Read",
            ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
        });

        GraphMentionUserDirectory directory = CreateDirectory(httpClient, tokenAcquisition.Object, configuration);

        IReadOnlyList<MentionUser> users = await directory.SearchAsync(
            "O'Neil",
            5,
            CancellationToken.None);

        Assert.AreEqual(2, users.Count);
        Assert.AreEqual("u1", users[0].Id);
        Assert.AreEqual("u1@contoso.com", users[0].Email);
        Assert.AreEqual("u2@contoso.com", users[1].Email);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.AreEqual("token-123", capturedRequest.Headers.Authorization?.Parameter);
        StringAssert.Contains(Uri.UnescapeDataString(capturedRequest.RequestUri!.ToString()), "startswith(displayName,'O''Neil')");
    }

    [TestMethod]
    public async Task GetAuthorizedUsersAsync_EmptyIds_ReturnsEmptyWithoutHttpCall()
    {
        int callCount = 0;
        HttpClient httpClient = new(new StubHttpMessageHandler((_, _) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));

        Mock<ITokenAcquisition> tokenAcquisition = new();
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Graph:Scopes:0"] = "User.Read",
            ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
        });

        GraphMentionUserDirectory directory = CreateDirectory(httpClient, tokenAcquisition.Object, configuration);

        IReadOnlyList<MentionUser> users = await directory.GetAuthorizedUsersAsync(
            (IReadOnlyCollection<string>)Array.Empty<string>(),
            CancellationToken.None);

        Assert.AreEqual(0, users.Count);
        Assert.AreEqual(0, callCount);
    }

    [TestMethod]
    public async Task GetAuthorizedUsersAsync_UsesIdFilterAndReturnsMappedUsers()
    {
        HttpRequestMessage? capturedRequest = null;
        HttpClient httpClient = new(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            const string json = """
                {
                  "value": [
                    { "id": "u1", "displayName": "User One", "mail": "u1@contoso.com", "userPrincipalName": "u1@contoso.com" }
                  ]
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }));

        Mock<ITokenAcquisition> tokenAcquisition = new();
        tokenAcquisition
            .Setup(acquisition => acquisition.GetAccessTokenForUserAsync(
                It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync("token-abc");

        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Graph:Scopes:0"] = "User.Read",
            ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
        });

        GraphMentionUserDirectory directory = CreateDirectory(httpClient, tokenAcquisition.Object, configuration);

        IReadOnlyList<MentionUser> users = await directory.GetAuthorizedUsersAsync(
            (IReadOnlyCollection<string>)new[] { "u1", "u2" },
            CancellationToken.None);

        Assert.AreEqual(1, users.Count);
        Assert.AreEqual("u1", users[0].Id);
        Assert.IsNotNull(capturedRequest);
        string decoded = Uri.UnescapeDataString(capturedRequest!.RequestUri!.ToString());
        StringAssert.Contains(decoded, "id eq 'u1' or id eq 'u2'");
    }

    [TestMethod]
    public async Task SearchAsync_InvalidPayload_ThrowsInvalidOperationException()
    {
        HttpClient httpClient = new(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"invalid\":[]}", Encoding.UTF8, "application/json")
            })));

        Mock<ITokenAcquisition> tokenAcquisition = new();
        tokenAcquisition
            .Setup(acquisition => acquisition.GetAccessTokenForUserAsync(
                It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync("token-xyz");

        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Graph:Scopes:0"] = "User.Read",
            ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
        });

        GraphMentionUserDirectory directory = CreateDirectory(httpClient, tokenAcquisition.Object, configuration);

        InvalidOperationException exception = await AssertThrowsAsync<InvalidOperationException>(() =>
            directory.SearchAsync("user", 5, CancellationToken.None));

        StringAssert.Contains(exception.Message, "invalid user collection response");
    }

    private static GraphMentionUserDirectory CreateDirectory(
        HttpClient httpClient,
        ITokenAcquisition tokenAcquisition,
        IConfiguration configuration)
    {
        return new GraphMentionUserDirectory(
            httpClient,
            tokenAcquisition,
            configuration,
            NullLogger<GraphMentionUserDirectory>.Instance);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
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

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }
        catch (TException exception)
        {
            return exception;
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
