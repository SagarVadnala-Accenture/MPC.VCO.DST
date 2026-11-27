using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services;

namespace MPC.VCO.DST.Tests.UI.Services;

[TestClass]
public sealed class PlanningCyclesApiClientTests
{
    [TestMethod]
    public async Task GetPlanningCycleAsync_MatchingCycle_ReturnsMappedBestModelValues()
    {
        PlanningCyclesApiClient client = CreateClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("https://api.local/api/planning-cycles?year=2026&month=7", request.RequestUri!.ToString());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<PlanningCycleApiResponse>
                {
                    new()
                    {
                        PlanningCycleId = "PC-2026-07",
                        Year = 2026,
                        Month = 7,
                        BestModel = new BestModelApiResponse
                        {
                            ModelId = "M-001",
                            ExpectedValueMonthlyUsd = 247900m
                        }
                    }
                })
            });
        });

        PlanningCycle result = await client.GetPlanningCycleAsync(new DateOnly(2026, 7, 1), CancellationToken.None);

        Assert.AreEqual("PC-2026-07", result.PlanningCycleId);
        Assert.AreEqual("M-001", result.BestModelId);
        Assert.IsNull(result.Recovery);
        Assert.AreEqual(248, result.ExpectedValue);
    }

    [TestMethod]
    public async Task GetPlanningCycleAsync_NoMatchingCycle_ReturnsEmptyCycleForPeriod()
    {
        PlanningCyclesApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<PlanningCycleApiResponse>
                {
                    new() { PlanningCycleId = "PC-2026-06", Year = 2026, Month = 6 }
                })
            }));

        PlanningCycle result = await client.GetPlanningCycleAsync(new DateOnly(2026, 7, 1), CancellationToken.None);

        Assert.AreEqual(string.Empty, result.PlanningCycleId);
        Assert.AreEqual(new DateOnly(2026, 7, 1), result.Period);
        Assert.IsNull(result.BestModelId);
        Assert.IsNull(result.Recovery);
        Assert.IsNull(result.ExpectedValue);
    }

    [TestMethod]
    public async Task GetPlanningCycleAsync_ErrorStatus_ThrowsHttpRequestException()
    {
        HttpStatusCode[] statusCodes =
        [
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError
        ];

        foreach (HttpStatusCode statusCode in statusCodes)
        {
            PlanningCyclesApiClient client = CreateClient((_, _) =>
                Task.FromResult(new HttpResponseMessage(statusCode)));

            HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
                client.GetPlanningCycleAsync(new DateOnly(2026, 7, 1), CancellationToken.None));

            Assert.AreEqual(statusCode, exception.StatusCode);
        }
    }

    [TestMethod]
    public async Task GetPlanningCycleAsync_MalformedPayload_ThrowsJsonException()
    {
        PlanningCyclesApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json")
            }));

        await AssertThrowsAsync<System.Text.Json.JsonException>(() =>
            client.GetPlanningCycleAsync(new DateOnly(2026, 7, 1), CancellationToken.None));
    }

    [TestMethod]
    public async Task GetPlanningCycleAsync_CanceledToken_ThrowsTaskCanceledException()
    {
        PlanningCyclesApiClient client = CreateClient((_, cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(cancellationToken));

        CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        await AssertThrowsAsync<TaskCanceledException>(() =>
            client.GetPlanningCycleAsync(new DateOnly(2026, 7, 1), cancellationTokenSource.Token));
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

    private static PlanningCyclesApiClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));

        AuthenticationStateProvider authStateProvider = new StubAuthenticationStateProvider(user);
        TestNavigationManager navigationManager = new();
        DstApiAuthenticatedRequestSender requestSender = new(authStateProvider, navigationManager);

        HttpClient httpClient = new(new RecordingHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://api.local/")
        };

        return new PlanningCyclesApiClient(httpClient, requestSender, Mock.Of<ILogger<PlanningCyclesApiClient>>());
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
