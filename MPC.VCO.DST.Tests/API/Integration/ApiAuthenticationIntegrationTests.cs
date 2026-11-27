using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPC.VCO.DST.WebSite.API;

namespace MPC.VCO.DST.Tests.API.Integration;

[TestClass]
[DoNotParallelize]
public sealed class ApiAuthenticationIntegrationTests
{
    private static readonly WebApplicationFactory<Program> Factory = new TestApiFactory();

    static ApiAuthenticationIntegrationTests()
    {
        Environment.SetEnvironmentVariable("CosmosDb__Endpoint", "https://localhost:8081/");
        Environment.SetEnvironmentVariable("CosmosDb__Key", "C2y6yDjf5/R+ob0N8A7Cgv30VRM=");
        Environment.SetEnvironmentVariable("AzureAd__TenantId", "test-tenant");
        Environment.SetEnvironmentVariable("AzureAd__ClientId", "test-client");
        Environment.SetEnvironmentVariable("AzureAd__Audience", "api://test-client");
        Environment.SetEnvironmentVariable("AzureAd__Scopes__0", "api://test-client/access_as_user");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "https://localhost");
        Environment.SetEnvironmentVariable("Graph__Scopes__0", "User.Read");
        Environment.SetEnvironmentVariable("MentionUsers__AllowedGroupId", Guid.NewGuid().ToString());
    }

    [TestMethod]
    public async Task GetPlanningCyclesAsync_AnonymousRequest_ReturnsUnauthorized()
    {
        using HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/planning-cycles?year=2026&month=7");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPlanningCyclesAsync_AuthenticatedRequest_ReturnsOk()
    {
        using HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "valid");

        HttpResponseMessage response = await client.GetAsync("/api/planning-cycles?year=2026&month=7");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        IReadOnlyList<PlanningCycleResponse>? payload =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<PlanningCycleResponse>>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(1, payload.Count);
        Assert.AreEqual("PC-2026-07", payload[0].PlanningCycleId);
    }

    [TestMethod]
    public async Task CreateModelAsync_InvalidRequest_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/planning-cycles/PC-2026-07/models",
            new CreateModelRequest
            {
                Status = "Invalid",
                Scope = new ModelScopeRequest { Type = "A", Name = "B" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.1m,
                NaturalGasPriceUsdPerMmbtu = 2.2m
            });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetails? payload =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(400, payload.Status);
        Assert.AreEqual("Bad Request", payload.Title);
    }

    [TestMethod]
    public async Task CreateModelAsync_UnexpectedError_ReturnsInternalServerErrorProblemDetails()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/planning-cycles/PC-2026-07/models",
            new CreateModelRequest
            {
                Status = "Crash",
                Scope = new ModelScopeRequest { Type = "A", Name = "B" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.1m,
                NaturalGasPriceUsdPerMmbtu = 2.2m
            });

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);

        ProblemDetails? payload =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(500, payload.Status);
        Assert.AreEqual("Internal Server Error", payload.Title);
        Assert.AreEqual("An unexpected error occurred.", payload.Detail);
    }

    [TestMethod]
    public async Task GetModelsAsync_AuthenticatedRequest_ReturnsOk()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response =
            await client.GetAsync("/api/planning-cycles/PC-2026-07/models?pageNumber=1&pageSize=10");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        IReadOnlyList<ModelResponse>? payload =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<ModelResponse>>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(1, payload.Count);
        Assert.AreEqual("PC-2026-07-001", payload[0].ModelId);
    }

    [TestMethod]
    public async Task UpdateModelAsync_AuthenticatedRequest_ReturnsOk()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/planning-cycles/PC-2026-07/models/PC-2026-07-001",
            new UpdateModelRequest
            {
                Status = "Reviewed",
                Scope = new ModelScopeRequest { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.1m,
                NaturalGasPriceUsdPerMmbtu = 2.2m
            });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task UpdateModelAsync_MissingModel_ReturnsNotFound()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/planning-cycles/PC-2026-07/models/MISSING",
            new UpdateModelRequest
            {
                Status = "Reviewed",
                Scope = new ModelScopeRequest { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.1m,
                NaturalGasPriceUsdPerMmbtu = 2.2m
            });

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteModelAsync_ExistingModel_ReturnsNoContent()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response =
            await client.DeleteAsync("/api/planning-cycles/PC-2026-07/models/PC-2026-07-001");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteModelAsync_MissingModel_ReturnsNotFound()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response =
            await client.DeleteAsync("/api/planning-cycles/PC-2026-07/models/MISSING");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetModelNotesAsync_AuthenticatedRequest_ReturnsOkWithContinuationToken()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync("/api/models/PC-2026-07-001/notes?pageSize=20");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.TryGetValues("x-continuation-token", out IEnumerable<string>? headerValues));
        Assert.AreEqual("next-page-token", headerValues!.Single());

        IReadOnlyList<ModelNoteResponse>? payload =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<ModelNoteResponse>>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(1, payload.Count);
    }

    [TestMethod]
    public async Task GetModelNotesAsync_InvalidPageSize_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync("/api/models/PC-2026-07-001/notes?pageSize=0");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetails? payload =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(400, payload.Status);
        Assert.AreEqual("Bad Request", payload.Title);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_AuthenticatedRequest_ReturnsCreated()
    {
        using HttpClient client = CreateAuthenticatedClient();
        FakeModelNoteService.ResetCreateCallCount();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/models/PC-2026-07-001/notes",
            new
            {
                NoteText = "Integration note",
                Mentions = Array.Empty<object>()
            });

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        ModelNoteResponse? payload = await response.Content.ReadFromJsonAsync<ModelNoteResponse>();
        Assert.IsNotNull(payload);
        Assert.AreEqual("Integration note", payload.NoteText);
        Assert.AreEqual("test-user", payload.Author.UserId);
        Assert.AreEqual(1, FakeModelNoteService.CreateCallCount);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_MoreThanTwentyUniqueMentions_ReturnsBadRequestValidationProblem()
    {
        using HttpClient client = CreateAuthenticatedClient();
        FakeModelNoteService.ResetCreateCallCount();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/models/PC-2026-07-001/notes",
            new
            {
                NoteText = "Integration note",
                Mentions = Enumerable.Range(1, 21).Select(index => new
                {
                    UserId = $"u-{index}",
                    Start = index,
                    Length = 2
                }).ToArray()
            });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        ValidationProblemDetails? payload =
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.IsNotNull(payload);
        Assert.IsTrue(payload.Errors.ContainsKey("Mentions"));
        CollectionAssert.Contains(
            payload.Errors["Mentions"],
            "A note cannot mention more than 20 users.");
        Assert.AreEqual(0, FakeModelNoteService.CreateCallCount);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_TwentyUniqueMentionsWithDuplicatesAndBlanks_ReturnsCreated()
    {
        using HttpClient client = CreateAuthenticatedClient();
        FakeModelNoteService.ResetCreateCallCount();

        List<object> mentions =
        [
            new { UserId = "  u-1  ", Start = 1, Length = 2 },
            new { UserId = "U-1", Start = 2, Length = 2 },
            new { UserId = "", Start = 3, Length = 2 },
            new { UserId = "   ", Start = 4, Length = 2 }
        ];

        mentions.AddRange(
            Enumerable.Range(2, 19).Select(index => (object)new
            {
                UserId = $"u-{index}",
                Start = index + 10,
                Length = 2
            }));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/models/PC-2026-07-001/notes",
            new
            {
                NoteText = "Integration note",
                Mentions = mentions
            });

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(1, FakeModelNoteService.CreateCallCount);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_InvalidRequest_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/models/PC-2026-07-001/notes",
            new
            {
                NoteText = "",
                Mentions = Array.Empty<object>()
            });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetails? payload =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(400, payload.Status);
        Assert.AreEqual("Bad Request", payload.Title);
    }

    [TestMethod]
    public async Task SearchMentionUsersAsync_InvalidSearch_ReturnsBadRequestProblemDetails()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync("/api/users/mentions?search=invalid");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetails? payload =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(400, payload.Status);
        Assert.AreEqual("Bad Request", payload.Title);
    }

    [TestMethod]
    public async Task SearchMentionUsersAsync_GraphFailure_ReturnsBadGatewayProblemDetails()
    {
        using HttpClient client = CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync("/api/users/mentions?search=graph-fail");

        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);

        ProblemDetails? payload =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.IsNotNull(payload);
        Assert.AreEqual(502, payload.Status);
        Assert.AreEqual("Bad Gateway", payload.Title);
        Assert.AreEqual("User search is temporarily unavailable.", payload.Detail);
    }

    private static HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "valid");
        return client;
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                    ["AzureAd:TenantId"] = "test-tenant",
                    ["AzureAd:ClientId"] = "test-client",
                    ["AzureAd:Audience"] = "api://test-client",
                    ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                    ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM=",
                    ["Cors:AllowedOrigins:0"] = "https://localhost",
                    ["Graph:Scopes:0"] = "User.Read",
                    ["MentionUsers:AllowedGroupId"] = Guid.NewGuid().ToString()
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlanningCycleService>();
                services.RemoveAll<IModelService>();
                services.RemoveAll<IModelNoteService>();
                services.RemoveAll<IMentionUserService>();

                services.AddSingleton<IPlanningCycleService, FakePlanningCycleService>();
                services.AddSingleton<IModelService, FakeModelService>();
                services.AddSingleton<IModelNoteService, FakeModelNoteService>();
                services.AddSingleton<IMentionUserService, FakeMentionUserService>();

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
            });
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestScheme";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Auth", out var value)
                || !string.Equals(value.ToString(), "valid", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing or invalid test auth header."));
            }

            Claim[] claims =
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim("oid", "test-user"),
                new Claim("preferred_username", "test.user@company.com")
            ];

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class FakePlanningCycleService : IPlanningCycleService
    {
        public Task<IReadOnlyList<PlanningCycleResponse>> GetPlanningCyclesAsync(GetPlanningCyclesRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PlanningCycleResponse>>([
                new PlanningCycleResponse
                {
                    PlanningCycleId = "PC-2026-07",
                    Year = request.Year,
                    Month = request.Month,
                    Name = "July, 2026",
                    CycleState = "Current"
                }
            ]);
    }

    private sealed class FakeModelService : IModelService
    {
        public Task<IReadOnlyList<ModelResponse>> GetModelsAsync(GetModelsRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModelResponse>>([
                new ModelResponse
                {
                    ModelId = "PC-2026-07-001",
                    CreatedOn = new DateOnly(2026, 7, 1),
                    Status = "Draft",
                    Scope = new ModelScopeResponse { Type = "Refinery", Name = "TX" },
                    ModelType = "Type-A",
                    EthanePriceUsdPerGal = 1.1m,
                    NaturalGasPriceUsdPerMmbtu = 2.2m,
                    ExpectedValueMonthlyUsd = 0
                }
            ]);

        public Task<ModelResponse?> GetModelAsync(string planningCycleId, string modelId, CancellationToken cancellationToken = default)
            => Task.FromResult<ModelResponse?>(null);

        public Task<ModelResponse> CreateModelAsync(CreateModelRequest request, CancellationToken cancellationToken = default)
        {
            if (string.Equals(request.Status, "Crash", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected service failure details that must not be exposed.");
            }

            if (!string.Equals(request.Status, "Draft", StringComparison.Ordinal))
            {
                throw new ArgumentException("Status must be one of: Draft, Reviewed, Executable.");
            }

            return Task.FromResult(new ModelResponse
            {
                ModelId = "PC-2026-07-001",
                CreatedOn = new DateOnly(2026, 7, 1),
                Status = request.Status,
                Scope = new ModelScopeResponse
                {
                    Type = request.Scope.Type,
                    Name = request.Scope.Name
                },
                ModelType = request.ModelType,
                EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
                NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu,
                ExpectedValueMonthlyUsd = 0
            });
        }

        public Task<ModelResponse?> UpdateModelAsync(UpdateModelRequest request, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(request.Status, "Draft", StringComparison.Ordinal)
                && !string.Equals(request.Status, "Reviewed", StringComparison.Ordinal)
                && !string.Equals(request.Status, "Executable", StringComparison.Ordinal))
            {
                throw new ArgumentException("Status must be one of: Draft, Reviewed, Executable.");
            }

            if (string.Equals(request.ModelId, "MISSING", StringComparison.Ordinal))
            {
                return Task.FromResult<ModelResponse?>(null);
            }

            return Task.FromResult<ModelResponse?>(new ModelResponse
            {
                ModelId = request.ModelId,
                CreatedOn = new DateOnly(2026, 7, 1),
                Status = request.Status,
                Scope = new ModelScopeResponse
                {
                    Type = request.Scope.Type,
                    Name = request.Scope.Name
                },
                ModelType = request.ModelType,
                EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
                NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu,
                ExpectedValueMonthlyUsd = 0
            });
        }

        public Task<bool> DeleteModelAsync(string planningCycleId, string modelId, CancellationToken cancellationToken = default)
            => Task.FromResult(!string.Equals(modelId, "MISSING", StringComparison.Ordinal));
    }

    private sealed class FakeModelNoteService : IModelNoteService
    {
        private static int _createCallCount;

        public static int CreateCallCount => _createCallCount;

        public static void ResetCreateCallCount()
        {
            Interlocked.Exchange(ref _createCallCount, 0);
        }

        public Task<GetModelNotesResponse> GetModelNotesAsync(GetModelNotesRequest request, CancellationToken cancellationToken = default)
        {
            if (request.PageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.PageSize), "Page size must be greater than zero.");
            }

            return Task.FromResult(new GetModelNotesResponse
            {
                ContinuationToken = "next-page-token",
                Notes =
                [
                    new ModelNoteResponse
                    {
                        Id = "note-1",
                        ModelId = request.ModelId,
                        NoteText = "Existing note",
                        Author = new ModelNoteAuthorResponse
                        {
                            UserId = "test-user",
                            DisplayName = "Test User",
                            UserPrincipalName = "test.user@company.com",
                            Initials = "TU"
                        },
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        Mentions = []
                    }
                ]
            });
        }

        public Task<ModelNoteResponse> CreateModelNoteAsync(CreateModelNoteRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _createCallCount);

            if (string.IsNullOrWhiteSpace(request.NoteText))
            {
                throw new ArgumentException("Note text is required.");
            }

            return Task.FromResult(new ModelNoteResponse
            {
                Id = "note-1",
                ModelId = request.ModelId,
                NoteText = request.NoteText,
                Author = new ModelNoteAuthorResponse
                {
                    UserId = request.UserId,
                    DisplayName = request.DisplayName,
                    UserPrincipalName = request.UserPrincipalName,
                    Initials = request.Initials
                },
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Mentions = []
            });
        }
    }

    private sealed class FakeMentionUserService : IMentionUserService
    {
        public Task<IReadOnlyList<MentionUser>> SearchAsync(string search, CancellationToken cancellationToken = default)
        {
            if (string.Equals(search, "invalid", StringComparison.Ordinal))
            {
                throw new ArgumentException("Search cannot exceed 100 characters.");
            }

            if (string.Equals(search, "graph-fail", StringComparison.Ordinal))
            {
                throw new HttpRequestException("Graph unavailable");
            }

            return Task.FromResult<IReadOnlyList<MentionUser>>([
                new MentionUser("1", "Alice Doe", "alice@contoso.com")
            ]);
        }

        public Task<IReadOnlyList<MentionUser>> GetAuthorizedUsersAsync(
            IReadOnlyCollection<string> userIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MentionUser> users = userIds
                .Where(userId => !string.IsNullOrWhiteSpace(userId))
                .Select(userId => new MentionUser(userId, $"User {userId}", $"{userId}@contoso.com"))
                .ToArray();

            return Task.FromResult(users);
        }
    }
}
