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
public sealed class ModelsApiClientTests
{
    [TestMethod]
    public async Task GetModelsAsync_EmptyPlanningCycleId_ReturnsEmptyWithoutHttpCall()
    {
        int callCount = 0;
        ModelsApiClient client = CreateClient((_, _) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        IReadOnlyList<ModelRun> result = await client.GetModelsAsync(
            new PlanningCycle(string.Empty, new DateOnly(2026, 7, 1), 0, null, null, null),
            CancellationToken.None);

        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(0, callCount);
    }

    [TestMethod]
    public async Task GetModelsAsync_PaginatesAndMapsResponse()
    {
        int callCount = 0;

        ModelsApiClient client = CreateClient((request, _) =>
        {
            callCount++;
            Assert.AreEqual(HttpMethod.Get, request.Method);

            if (callCount == 1)
            {
                StringAssert.Contains(request.RequestUri!.ToString(), "pageNumber=1");

                List<ModelApiResponse> page1 = Enumerable.Range(1, 100)
                    .Select(index => CreateApiModel($"PC-2026-07-{index:000}", "Draft"))
                    .ToList();

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(page1)
                });
            }

            StringAssert.Contains(request.RequestUri!.ToString(), "pageNumber=2");
            List<ModelApiResponse> page2 =
            [
                CreateApiModel("PC-2026-07-101", "Reviewed")
            ];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(page2)
            });
        });

        IReadOnlyList<ModelRun> result = await client.GetModelsAsync(
            new PlanningCycle("PC-2026-07", new DateOnly(2026, 7, 1), 0, null, null, null),
            CancellationToken.None);

        Assert.AreEqual(101, result.Count);
        Assert.AreEqual("PC-2026-07-001", result[0].Id);
        Assert.AreEqual("PC-2026-07-101", result[^1].Id);
        Assert.AreEqual(ModelStatus.Reviewed, result[^1].Status);
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public async Task GetModelsAsync_EmptyFirstPage_ReturnsEmpty()
    {
        ModelsApiClient client = CreateClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<ModelApiResponse>())
            });
        });

        IReadOnlyList<ModelRun> result = await client.GetModelsAsync(
            new PlanningCycle("PC-2026-07", new DateOnly(2026, 7, 1), 0, null, null, null),
            CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetModelAsync_NotFound_ReturnsNull()
    {
        ModelsApiClient client = CreateClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("https://api.local/api/planning-cycles/PC-2026-07/models/M-001", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        ModelRun? result = await client.GetModelAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetModelAsync_BadRequest_ThrowsHttpRequestException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.GetModelAsync("PC-2026-07", "M-001", CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [TestMethod]
    public async Task GetModelAsync_NullPayload_ReturnsNull()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));

        ModelRun? result = await client.GetModelAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetModelAsync_InvalidPlanningCycleFormat_UsesCurrentPeriodFallback()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateApiModel("M-001", "Draft"))
            }));

        ModelRun? result = await client.GetModelAsync("invalid-cycle-id", "M-001", CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(DateTimeOffset.UtcNow.Year, result.Period.Year);
        Assert.AreEqual(DateTimeOffset.UtcNow.Month, result.Period.Month);
    }

    [TestMethod]
    public async Task GetModelAsync_MalformedPayload_ThrowsJsonException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json")
            }));

        await AssertThrowsAsync<System.Text.Json.JsonException>(() =>
            client.GetModelAsync("PC-2026-07", "M-001", CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelAsync_SendsPostAndMapsResponse()
    {
        string? payload = null;

        ModelsApiClient client = CreateClient(async (request, _) =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("https://api.local/api/planning-cycles/PC-2026-07/models", request.RequestUri!.ToString());
            payload = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateApiModel("PC-2026-07-001", "Executable"))
            };
        });

        ModelRun result = await client.CreateModelAsync(
            "PC-2026-07",
            new ModelUpsertRequest
            {
                Status = "Executable",
                ScopeType = "Refinery",
                ScopeName = "TX",
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.23m,
                NaturalGasPriceUsdPerMmbtu = 2.34m
            },
            CancellationToken.None);

        Assert.AreEqual("PC-2026-07-001", result.Id);
        Assert.AreEqual(ModelStatus.Executable, result.Status);
        Assert.IsNotNull(payload);
        StringAssert.Contains(payload, "\"status\":\"Executable\"");
        StringAssert.Contains(payload, "\"modelType\":\"Type-A\"");
    }

    [TestMethod]
    public async Task CreateModelAsync_UnknownStatus_MapsToDraft()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateApiModel("PC-2026-07-001", "Unknown"))
            }));

        ModelRun result = await client.CreateModelAsync(
            "PC-2026-07",
            CreateValidUpsertRequest(),
            CancellationToken.None);

        Assert.AreEqual(ModelStatus.Draft, result.Status);
    }

    [TestMethod]
    public async Task CreateModelAsync_Unauthorized_ThrowsHttpRequestException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.CreateModelAsync("PC-2026-07", CreateValidUpsertRequest(), CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [TestMethod]
    public async Task UpdateModelAsync_NotFound_ReturnsNull()
    {
        ModelsApiClient client = CreateClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Put, request.Method);
            Assert.AreEqual("https://api.local/api/planning-cycles/PC-2026-07/models/M-001", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        ModelRun? result = await client.UpdateModelAsync(
            "PC-2026-07",
            "M-001",
            new ModelUpsertRequest
            {
                Status = "Draft",
                ScopeType = "Refinery",
                ScopeName = "TX",
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.23m,
                NaturalGasPriceUsdPerMmbtu = 2.34m
            },
            CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateModelAsync_EmptyPayload_ThrowsInvalidOperationException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));

        await AssertThrowsAsync<InvalidOperationException>(() =>
            client.UpdateModelAsync(
                "PC-2026-07",
                "M-001",
                new ModelUpsertRequest
                {
                    Status = "Draft",
                    ScopeType = "Refinery",
                    ScopeName = "TX",
                    ModelType = "Type-A",
                    EthanePriceUsdPerGal = 1.23m,
                    NaturalGasPriceUsdPerMmbtu = 2.34m
                },
                CancellationToken.None));
    }

    [TestMethod]
    public async Task UpdateModelAsync_Forbidden_ThrowsHttpRequestException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));

        HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.UpdateModelAsync(
                "PC-2026-07",
                "M-001",
                CreateValidUpsertRequest(),
                CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_TrimmedText_SendsPostAndMapsResponse()
    {
        string? payload = null;

        ModelsApiClient client = CreateClient(async (request, _) =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("https://api.local/api/models/M-001/notes", request.RequestUri!.ToString());
            payload = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ModelNoteApiResponse
                {
                    Id = "note-1",
                    ModelId = "M-001",
                    NoteText = "hello @Jane Doe",
                    Author = new ModelNoteAuthorApiResponse
                    {
                        UserId = "u1",
                        DisplayName = "Jane Doe",
                        UserPrincipalName = "jane@contoso.com",
                        Initials = "JD"
                    },
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    Mentions =
                    [
                        new ModelNoteMentionApiResponse
                        {
                            UserId = "u1",
                            DisplayName = "Jane Doe",
                            Email = "jane@contoso.com",
                            Start = 6,
                            Length = 9
                        }
                    ]
                })
            };
        });

        ModelNote result = await client.CreateModelNoteAsync(
            "PC-2026-07",
            "M-001",
            new CreateModelNoteRequest(
                "  hello @Jane Doe  ",
                [new NoteMention("u1", "Jane Doe", "jane@contoso.com", 6, 9)]),
            CancellationToken.None);

        Assert.AreEqual("note-1", result.NoteId);
        Assert.AreEqual("hello @Jane Doe", result.Text);
        Assert.AreEqual(1, result.Mentions.Count);
        Assert.IsNotNull(payload);
        StringAssert.Contains(payload, "\"noteText\":\"hello @Jane Doe\"");
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_EmptyText_ThrowsArgumentException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        await AssertThrowsAsync<ArgumentException>(() =>
            client.CreateModelNoteAsync(
                "PC-2026-07",
                "M-001",
                new CreateModelNoteRequest("   ", []),
                CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_ServerError_ThrowsHttpRequestException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.CreateModelNoteAsync(
                "PC-2026-07",
                "M-001",
                new CreateModelNoteRequest("hello", []),
                CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_EmptyPayload_ThrowsInvalidOperationException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));

        await AssertThrowsAsync<InvalidOperationException>(() =>
            client.CreateModelNoteAsync(
                "PC-2026-07",
                "M-001",
                new CreateModelNoteRequest("hello", []),
                CancellationToken.None));
    }

    [TestMethod]
    public async Task DeleteModelAsync_NotFound_ReturnsFalse()
    {
        ModelsApiClient client = CreateClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Delete, request.Method);
            Assert.AreEqual("https://api.local/api/planning-cycles/PC-2026-07/models/M-001", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        bool deleted = await client.DeleteModelAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.IsFalse(deleted);
    }

    [TestMethod]
    public async Task DeleteModelAsync_Success_ReturnsTrue()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        bool deleted = await client.DeleteModelAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.IsTrue(deleted);
    }

    [TestMethod]
    public async Task DeleteModelAsync_ServerError_ThrowsHttpRequestException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.DeleteModelAsync("PC-2026-07", "M-001", CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [TestMethod]
    public async Task SearchMentionUsersAsync_EncodesSearchAndMapsUsers()
    {
        ModelsApiClient client = CreateClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            StringAssert.Contains(request.RequestUri!.ToString(), "https://api.local/api/users/mentions?search=");
            StringAssert.Contains(request.RequestUri!.ToString(), "Jane");
            StringAssert.Contains(request.RequestUri!.ToString(), "Doe%2Bteam");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<MentionUserApiResponse>
                {
                    new() { Id = "u1", DisplayName = "Jane Doe", Email = "jane@contoso.com" }
                })
            });
        });

        IReadOnlyList<MentionUser> result = await client.SearchMentionUsersAsync("Jane Doe+team", CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("u1", result[0].Id);
    }

    [TestMethod]
    public async Task SearchMentionUsersAsync_EmptyPayload_ReturnsEmpty()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));

        IReadOnlyList<MentionUser> result = await client.SearchMentionUsersAsync("term", CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task SearchMentionUsersAsync_Unauthorized_ThrowsHttpRequestException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.SearchMentionUsersAsync("term", CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [TestMethod]
    public async Task GetModelNotesAsync_MapsNotesAndMentions()
    {
        ModelsApiClient client = CreateClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("https://api.local/api/models/M-001/notes", request.RequestUri!.ToString());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<ModelNoteApiResponse>
                {
                    new()
                    {
                        Id = "note-1",
                        ModelId = "M-001",
                        NoteText = "text",
                        Author = new ModelNoteAuthorApiResponse
                        {
                            DisplayName = "Jane Doe",
                            UserPrincipalName = "jane@contoso.com"
                        },
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        Mentions =
                        [
                            new ModelNoteMentionApiResponse
                            {
                                UserId = "u1",
                                DisplayName = "Jane Doe",
                                Email = "jane@contoso.com",
                                Start = 0,
                                Length = 4
                            }
                        ]
                    }
                })
            });
        });

        IReadOnlyList<ModelNote> result = await client.GetModelNotesAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("note-1", result[0].NoteId);
        Assert.AreEqual(1, result[0].Mentions.Count);
    }

    [TestMethod]
    public async Task GetModelNotesAsync_EmptyPayload_ReturnsEmpty()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));

        IReadOnlyList<ModelNote> result = await client.GetModelNotesAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetModelNotesAsync_NotFound_ThrowsHttpRequestException()
    {
        ModelsApiClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        HttpRequestException exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.GetModelNotesAsync("PC-2026-07", "M-001", CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [TestMethod]
    public async Task GetModelsAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        ModelsApiClient client = CreateClient((_, cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(cancellationToken));

        CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        await AssertThrowsAsync<TaskCanceledException>(() =>
            client.GetModelsAsync(
                new PlanningCycle("PC-2026-07", new DateOnly(2026, 7, 1), 0, null, null, null),
                cancellationTokenSource.Token));
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

    private static ModelsApiClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
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

        return new ModelsApiClient(httpClient, requestSender, Mock.Of<ILogger<ModelsApiClient>>());
    }

    private static ModelApiResponse CreateApiModel(string modelId, string status)
    {
        return new ModelApiResponse
        {
            ModelId = modelId,
            CreatedOn = new DateOnly(2026, 7, 1),
            Status = status,
            Scope = new ModelScopeApiResponse { Type = "Refinery", Name = "TX" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m,
            AverageRecoveryPercent = 66.6m,
            ExpectedValueMonthlyUsd = 219000m
        };
    }

    private static ModelUpsertRequest CreateValidUpsertRequest() =>
        new()
        {
            Status = "Draft",
            ScopeType = "Refinery",
            ScopeName = "TX",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.23m,
            NaturalGasPriceUsdPerMmbtu = 2.34m
        };

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
