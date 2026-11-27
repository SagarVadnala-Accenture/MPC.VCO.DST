using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MPC.VCO.DST.WebSite.Services;

namespace MPC.VCO.DST.Tests.UI.Services;

[TestClass]
public sealed class DstApiAuthenticatedRequestSenderTests
{
    [TestMethod]
    public async Task SendAsync_UnauthenticatedUser_ThrowsInvalidOperationException()
    {
        AuthenticationStateProvider authStateProvider =
            new StubAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity()));

        TestNavigationManager navigationManager = new();
        DstApiAuthenticatedRequestSender sender = new(authStateProvider, navigationManager);

        using HttpClient httpClient = new(new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.local/")
        };

        await AssertThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(httpClient, HttpMethod.Get, "api/planning-cycles?year=2026&month=7", CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_AuthenticatedUser_SendsHttpRequest()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));

        AuthenticationStateProvider authStateProvider =
            new StubAuthenticationStateProvider(user);

        TestNavigationManager navigationManager = new();
        DstApiAuthenticatedRequestSender sender = new(authStateProvider, navigationManager);

        HttpRequestMessage? capturedRequest = null;

        using HttpClient httpClient = new(new RecordingHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Accepted));
        }))
        {
            BaseAddress = new Uri("https://api.local/")
        };

        using HttpResponseMessage response = await sender.SendAsync(
            httpClient,
            HttpMethod.Post,
            "api/planning-cycles/PC-2026-07/models",
            CancellationToken.None,
            new StringContent("{}"));

        Assert.AreEqual(System.Net.HttpStatusCode.Accepted, response.StatusCode);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual("https://api.local/api/planning-cycles/PC-2026-07/models", capturedRequest.RequestUri!.ToString());
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
        }
        catch (TException)
        {
        }
    }

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public StubAuthenticationStateProvider(ClaimsPrincipal user)
        {
            _user = user;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://app.local/", "https://app.local/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
