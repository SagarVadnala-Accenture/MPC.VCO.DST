using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using Moq;
using MPC.VCO.DST.WebSite.Services;

namespace MPC.VCO.DST.Tests.UI.Services;

[TestClass]
public sealed class DstApiAccessTokenHandlerTests
{
    [TestMethod]
    public void Constructor_MissingScopes_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Mock<ITokenAcquisition> tokenAcquisition = new();

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(() =>
            _ = new DstApiAccessTokenHandler(tokenAcquisition.Object, configuration));

        StringAssert.Contains(exception.Message, "DstApi:Scopes");
    }

    [TestMethod]
    public async Task SendAsync_AuthenticatedUser_AddsBearerTokenHeader()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DstApi:Scopes:0"] = "api://dst/read"
            })
            .Build();

        Mock<ITokenAcquisition> tokenAcquisition = new();
        tokenAcquisition
            .Setup(x => x.GetAccessTokenForUserAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<TokenAcquisitionOptions?>()))
            .ReturnsAsync("fake-token");

        HttpRequestMessage? capturedRequest = null;

        DstApiAccessTokenHandler handler = new(tokenAcquisition.Object, configuration)
        {
            InnerHandler = new RecordingHandler((request, _) =>
            {
                capturedRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            })
        };

        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, "https://api.local/planning-cycles");

        SetAuthenticatedUserOption(request);

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(capturedRequest);
        Assert.IsNotNull(capturedRequest.Headers.Authorization);
        Assert.AreEqual("Bearer", capturedRequest.Headers.Authorization!.Scheme);

        Assert.IsTrue(tokenAcquisition.Invocations.Count > 0);
    }

    [TestMethod]
    public async Task SendAsync_AnonymousUser_ThrowsInvalidOperationException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DstApi:Scopes:0"] = "api://dst/read"
            })
            .Build();

        Mock<ITokenAcquisition> tokenAcquisition = new();

        DstApiAccessTokenHandler handler = new(tokenAcquisition.Object, configuration)
        {
            InnerHandler = new RecordingHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
        };

        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, "https://api.local/planning-cycles");

        InvalidOperationException exception = await AssertThrowsAsync<InvalidOperationException>(() =>
            invoker.SendAsync(request, CancellationToken.None));

        StringAssert.Contains(exception.Message, "authenticated user");
    }

    private static void SetAuthenticatedUserOption(HttpRequestMessage request)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));

        FieldInfo? field = typeof(DstApiAuthenticatedRequestSender)
            .GetField("UserOptionKey", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);

        Assert.IsNotNull(field);
        object? optionKey = field!.GetValue(null);
        Assert.IsNotNull(optionKey);

        MethodInfo? setMethod = typeof(HttpRequestOptions)
            .GetMethods()
            .FirstOrDefault(method => method.Name == "Set" && method.IsGenericMethod);

        Assert.IsNotNull(setMethod);
        MethodInfo genericSet = setMethod!.MakeGenericMethod(typeof(ClaimsPrincipal));
        genericSet.Invoke(request.Options, [optionKey, user]);
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

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
