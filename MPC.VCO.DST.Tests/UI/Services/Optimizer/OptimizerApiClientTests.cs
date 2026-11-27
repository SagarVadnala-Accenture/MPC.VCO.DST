using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using MPC.VCO.DST.WebSite.Services;
using MPC.VCO.DST.WebSite.Services.Optimizer;
using MPC.VCO.DST.WebSite.Services.Optimizer.Models;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace MPC.VCO.DST.Tests.UI.Services.Optimizer;

[TestClass]
public sealed class OptimizerApiClientTests
{
    [TestMethod]
    public async Task OptimizeAsync_UsesIntegratedDstApiRoute()
    {
        RecordingHttpMessageHandler handler = new((request, _) =>
        {
            Assert.AreEqual("https://optimizer.local/api/optimizer", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new OptimizeResponseDto { Status = "optimal" })
            });
        });

        OptimizerApiClient client = CreateClient(handler);

        OptimizeResponseDto response = await client.OptimizeAsync(new OptimizeRequestDto(), CancellationToken.None);

        Assert.AreEqual("optimal", response.Status);
    }

    [TestMethod]
    public async Task OptimizeAsync_ApiReturnsBadRequest_ThrowsOptimizerApiExceptionWithValidationErrors()
    {
        Dictionary<string, string[]> errors = new()
        {
            ["horizon_days"] = ["Must be positive"]
        };

        RecordingHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new ValidationProblemDetailsDto
                    {
                        Title = "Validation failed",
                        Errors = errors
                    }),
                    Encoding.UTF8,
                    "application/json")
            }));

        OptimizerApiClient client = CreateClient(handler);

        OptimizerApiException exception = await AssertThrowsAsync<OptimizerApiException>(() =>
            client.OptimizeAsync(new OptimizeRequestDto { HorizonDays = 0 }, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.AreEqual("Please correct optimizer input values.", exception.UserMessage);
        Assert.IsTrue(exception.ValidationErrors.ContainsKey("horizon_days"));
    }

    [TestMethod]
    public async Task OptimizeAsync_ApiReturnsUnprocessableEntity_ThrowsOptimizerApiExceptionWithApiDetail()
    {
        RecordingHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new ValidationProblemDetailsDto
                    {
                        Detail = "No feasible plan."
                    }),
                    Encoding.UTF8,
                    "application/json")
            }));

        OptimizerApiClient client = CreateClient(handler);

        OptimizerApiException exception = await AssertThrowsAsync<OptimizerApiException>(() =>
            client.OptimizeAsync(new OptimizeRequestDto { HorizonDays = 10 }, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.AreEqual("No feasible optimization solution was found for the selected settings.", exception.UserMessage);
        Assert.AreEqual("No feasible plan.", exception.ApiDetail);
    }

    [TestMethod]
    public async Task OptimizeAsync_EmptySuccessPayload_ThrowsJsonException()
    {
        RecordingHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("", Encoding.UTF8, "application/json")
            }));

        OptimizerApiClient client = CreateClient(handler);

        await AssertThrowsAsync<JsonException>(() =>
            client.OptimizeAsync(new OptimizeRequestDto { HorizonDays = 10 }, CancellationToken.None));
    }

    [TestMethod]
    public async Task OptimizeAsync_ConnectionFailure_PropagatesHttpRequestException()
    {
        RecordingHttpMessageHandler handler = new((_, _) => throw new HttpRequestException("Network down"));
        OptimizerApiClient client = CreateClient(handler);

        await AssertThrowsAsync<HttpRequestException>(() =>
            client.OptimizeAsync(new OptimizeRequestDto { HorizonDays = 10 }, CancellationToken.None));
    }

    private static OptimizerApiClient CreateClient(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://optimizer.local/")
        };

        return new OptimizerApiClient(
            httpClient,
            new DstApiAuthenticatedRequestSender(
                new StubAuthenticationStateProvider(),
                new StubNavigationManager()),
            Mock.Of<ILogger<OptimizerApiClient>>());
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

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly ClaimsPrincipal User = new(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "TestAuth"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(User));
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager() => Initialize("https://app.local/", "https://app.local/");

        protected override void NavigateToCore(string uri, bool forceLoad) =>
            Uri = ToAbsoluteUri(uri).ToString();
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
