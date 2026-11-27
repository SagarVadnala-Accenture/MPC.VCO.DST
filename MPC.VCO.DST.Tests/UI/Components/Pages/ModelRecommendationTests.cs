using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MPC.VCO.DST.WebSite.Components.Pages;
using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services;

namespace MPC.VCO.DST.Tests.UI.Components.Pages;

[TestClass]
public sealed class ModelRecommendationTests
{
    [TestMethod]
    public async Task OnParametersSetAsync_Success_LoadsModelAndSeedsUpdateRequest()
    {
        ModelsApiClient apiClient = CreateModelsApiClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateApiModelResponse())
            });
        });

        ModelRecommendation component = CreateComponent(apiClient, new TestNavigationManager());

        await InvokePrivateTask(component, "OnParametersSetAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_loading"));
        Assert.IsFalse(GetPrivateField<bool>(component, "_hasError"));

        ModelRun? model = GetPrivateProperty<ModelRun?>(component, "Model");
        Assert.IsNotNull(model);
        Assert.AreEqual("M-001", model.Id);

        ModelUpsertRequest request = GetPrivateField<ModelUpsertRequest>(component, "_updateRequest");
        Assert.AreEqual("Reviewed", request.Status);
        Assert.AreEqual("Refinery", request.ScopeType);
        Assert.AreEqual("TX", request.ScopeName);
        Assert.AreEqual("Type-A", request.ModelType);
        Assert.AreEqual(1.10m, request.EthanePriceUsdPerGal);
        Assert.AreEqual(2.20m, request.NaturalGasPriceUsdPerMmbtu);
    }

    [TestMethod]
    public async Task OnParametersSetAsync_HttpFailure_SetsErrorState()
    {
        ModelsApiClient apiClient = CreateModelsApiClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        ModelRecommendation component = CreateComponent(apiClient, new TestNavigationManager());

        await InvokePrivateTask(component, "OnParametersSetAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_loading"));
        Assert.IsTrue(GetPrivateField<bool>(component, "_hasError"));
        Assert.IsNull(GetPrivateProperty<ModelRun?>(component, "Model"));
    }

    [TestMethod]
    public async Task SaveAsync_Success_UpdatesModelAndResetsSaving()
    {
        int putCalls = 0;
        ModelsApiClient apiClient = CreateModelsApiClient((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(CreateApiModelResponse())
                });
            }

            if (request.Method == HttpMethod.Put)
            {
                putCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        modelId = "M-001",
                        createdOn = new DateOnly(2026, 7, 10),
                        status = "Executable",
                        scope = new { type = "Refinery", name = "TX" },
                        modelType = "Type-A",
                        ethanePriceUsdPerGal = 1.10m,
                        naturalGasPriceUsdPerMmbtu = 2.20m,
                        averageRecoveryPercent = 70.0m,
                        expectedValueMonthlyUsd = 250000m
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        ModelRecommendation component = CreateComponent(apiClient, new TestNavigationManager());
        await InvokePrivateTask(component, "OnParametersSetAsync");

        await InvokePrivateTask(component, "SaveAsync");

        Assert.AreEqual(1, putCalls);
        Assert.IsFalse(GetPrivateField<bool>(component, "_saving"));
        ModelRun? model = GetPrivateProperty<ModelRun?>(component, "Model");
        Assert.IsNotNull(model);
        Assert.AreEqual(ModelStatus.Executable, model.Status);
        Assert.AreEqual(250, model.ExpectedValue);
    }

    [TestMethod]
    public async Task SaveAsync_InvalidRequest_HandledAndSavingReset()
    {
        int apiCalls = 0;
        ModelsApiClient apiClient = CreateModelsApiClient((_, _) =>
        {
            apiCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateApiModelResponse())
            });
        });

        ModelRecommendation component = CreateComponent(apiClient, new TestNavigationManager());

        await InvokePrivateTask(component, "SaveAsync");

        Assert.AreEqual(0, apiCalls);
        Assert.IsFalse(GetPrivateField<bool>(component, "_saving"));
    }

    [TestMethod]
    public async Task DeleteAsync_Success_NavigatesToModelHistory()
    {
        ModelsApiClient apiClient = CreateModelsApiClient((request, _) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateApiModelResponse())
            });
        });

        TestNavigationManager navigation = new();
        ModelRecommendation component = CreateComponent(apiClient, navigation);

        await InvokePrivateTask(component, "DeleteAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_saving"));
        Assert.AreEqual("https://app.local/model-history", navigation.Uri);
    }

    [TestMethod]
    public async Task DeleteAsync_NotFound_DoesNotNavigate()
    {
        ModelsApiClient apiClient = CreateModelsApiClient((request, _) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateApiModelResponse())
            });
        });

        TestNavigationManager navigation = new();
        ModelRecommendation component = CreateComponent(apiClient, navigation);

        await InvokePrivateTask(component, "DeleteAsync");

        Assert.IsFalse(GetPrivateField<bool>(component, "_saving"));
        Assert.AreEqual("https://app.local/", navigation.Uri);
    }

    private static ModelRecommendation CreateComponent(ModelsApiClient apiClient, NavigationManager navigation)
    {
        ModelRecommendation component = new();

        SetInjectedProperty(component, "ModelsApiClient", apiClient);
        SetInjectedProperty(component, "Navigation", navigation);
        SetInjectedProperty(component, "Snackbar", new Mock<ISnackbar>().Object);
        SetInjectedProperty(component, nameof(ModelRecommendation.PlanningCycleId), "PC-2026-07");
        SetInjectedProperty(component, nameof(ModelRecommendation.Id), "M-001");

        return component;
    }

    private static object CreateApiModelResponse() => new
    {
        modelId = "M-001",
        createdOn = new DateOnly(2026, 7, 10),
        status = "Reviewed",
        scope = new { type = "Refinery", name = "TX" },
        modelType = "Type-A",
        ethanePriceUsdPerGal = 1.10m,
        naturalGasPriceUsdPerMmbtu = 2.20m,
        averageRecoveryPercent = 66.6m,
        expectedValueMonthlyUsd = 219000m
    };

    private static ModelsApiClient CreateModelsApiClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));

        AuthenticationStateProvider authProvider = new StubAuthenticationStateProvider(user);
        NavigationManager navigation = new TestNavigationManager();
        DstApiAuthenticatedRequestSender requestSender = new(authProvider, navigation);

        HttpClient httpClient = new(new RecordingHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://api.local/")
        };

        return new ModelsApiClient(httpClient, requestSender, NullLogger<ModelsApiClient>.Instance);
    }

    private static void SetInjectedProperty(object instance, string propertyName, object value)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(property, $"Property '{propertyName}' was not found.");
        property!.SetValue(instance, value);
    }

    private static async Task InvokePrivateTask(object instance, string methodName)
    {
        MethodInfo method = GetMethod(instance.GetType(), methodName);
        Task task = (Task)method.Invoke(instance, null)!;
        await task;
    }

    private static MethodInfo GetMethod(Type type, string methodName)
    {
        MethodInfo? method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
        return method!;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
        return (T)field!.GetValue(instance)!;
    }

    private static T GetPrivateProperty<T>(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(property, $"Property '{propertyName}' was not found.");
        return (T)property!.GetValue(instance)!;
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
}
