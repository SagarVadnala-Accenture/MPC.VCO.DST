using System.Security.Claims;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MPC.VCO.DST.WebSite.API.Controllers;

namespace MPC.VCO.DST.Tests.API.Controllers;

[TestClass]
public sealed class ModelNotesControllerTests
{
    [TestMethod]
    public async Task GetModelNotesAsync_ReturnsOk()
    {
        var service = new FakeModelNoteService();
        var controller = new ModelNotesController(service);

        ActionResult<IReadOnlyList<ModelNoteResponse>> result =
            await controller.GetModelNotesAsync("model-1", 20, null, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_ReturnsCreated()
    {
        var service = new FakeModelNoteService();
        var controller = new ModelNotesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("oid", "user-1"),
                        new Claim("name", "Jane Doe"),
                        new Claim("preferred_username", "jane.doe@contoso.com")
                    ], "TestAuth"))
                }
            }
        };

        ActionResult<ModelNoteResponse> result = await controller.CreateModelNoteAsync(
            "model-1",
            new PostModelNoteRequest { NoteText = " hello " },
            CancellationToken.None);

        Assert.IsInstanceOfType<CreatedAtRouteResult>(result.Result);
        var createdResult = (CreatedAtRouteResult)result.Result!;
        Assert.AreEqual("GetModelNotes", createdResult.RouteName);
        Assert.AreEqual("model-1", createdResult.RouteValues!["modelId"]);

        var payload = createdResult.Value as ModelNoteResponse;
        Assert.IsNotNull(payload);
        Assert.AreEqual("note-1", payload.Id);
        Assert.AreEqual("model-1", payload.ModelId);
        Assert.AreEqual("hello", payload.NoteText);

        Assert.AreEqual("hello", service.LastCreateRequest!.NoteText);
        Assert.AreEqual(1, service.CreateCalls);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_InvalidNote_ThrowsArgumentException()
    {
        var service = new FakeModelNoteService();
        var controller = new ModelNotesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("oid", "user-1"),
                        new Claim("name", "Jane Doe"),
                        new Claim("preferred_username", "jane.doe@contoso.com")
                    ], "TestAuth"))
                }
            }
        };

        await AssertThrowsAsync<ArgumentException>(() => controller.CreateModelNoteAsync(
            "model-1",
            new PostModelNoteRequest { NoteText = "   " },
            CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_MoreThanTwentyUniqueMentions_ReturnsValidationProblem()
    {
        var service = new FakeModelNoteService();
        var controller = new ModelNotesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("oid", "user-1"),
                        new Claim("name", "Jane Doe"),
                        new Claim("preferred_username", "jane.doe@contoso.com") 
                    ], "TestAuth"))
                }
            }
        };

        IReadOnlyList<NoteMentionRequest> mentions =
        [
            .. Enumerable.Range(1, 21).Select(index => new NoteMentionRequest
            {
                UserId = $"u-{index}",
                Start = index,
                Length = 2
            })
        ];

        ActionResult<ModelNoteResponse> result = await controller.CreateModelNoteAsync(
            "model-1",
            new PostModelNoteRequest
            {
                NoteText = "hello",
                Mentions = mentions
            },
            CancellationToken.None);

        ObjectResult? objectResult = result.Result as ObjectResult;
        Assert.IsNotNull(objectResult);

        ValidationProblemDetails? validationDetails = objectResult.Value as ValidationProblemDetails;
        Assert.IsNotNull(validationDetails);
        Assert.IsTrue(validationDetails.Errors.ContainsKey(nameof(PostModelNoteRequest.Mentions)));
        CollectionAssert.Contains(
            validationDetails.Errors[nameof(PostModelNoteRequest.Mentions)],
            "A note cannot mention more than 20 users.");

        Assert.AreEqual(0, service.CreateCalls);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_ExactlyTwentyUniqueMentions_ReturnsCreated()
    {
        var service = new FakeModelNoteService();
        var controller = new ModelNotesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("oid", "user-1"),
                        new Claim("name", "Jane Doe"),
                        new Claim("preferred_username", "jane.doe@contoso.com")
                    ], "TestAuth"))
                }
            }
        };

        IReadOnlyList<NoteMentionRequest> mentions =
        [
            .. Enumerable.Range(1, 20).Select(index => new NoteMentionRequest
            {
                UserId = $"u-{index}",
                Start = index,
                Length = 2
            })
        ];

        ActionResult<ModelNoteResponse> result = await controller.CreateModelNoteAsync(
            "model-1",
            new PostModelNoteRequest
            {
                NoteText = "hello",
                Mentions = mentions
            },
            CancellationToken.None);

        Assert.IsInstanceOfType<CreatedAtRouteResult>(result.Result);
        Assert.AreEqual(1, service.CreateCalls);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_MentionCount_IgnoresDuplicatesCaseAndBlankIds()
    {
        var service = new FakeModelNoteService();
        var controller = new ModelNotesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("oid", "user-1"),
                        new Claim("name", "Jane Doe"),
                        new Claim("preferred_username", "jane.doe@contoso.com")
                    ], "TestAuth"))
                }
            }
        };

        List<NoteMentionRequest> mentions =
        [
            new() { UserId = "  u-1  ", Start = 1, Length = 2 },
            new() { UserId = "U-1", Start = 2, Length = 2 },
            new() { UserId = "", Start = 3, Length = 2 },
            new() { UserId = "   ", Start = 4, Length = 2 }
        ];

        mentions.AddRange(
            Enumerable.Range(2, 19).Select(index => new NoteMentionRequest
            {
                UserId = $"u-{index}",
                Start = index + 10,
                Length = 2
            }));

        ActionResult<ModelNoteResponse> result = await controller.CreateModelNoteAsync(
            "model-1",
            new PostModelNoteRequest
            {
                NoteText = "hello",
                Mentions = mentions
            },
            CancellationToken.None);

        Assert.IsInstanceOfType<CreatedAtRouteResult>(result.Result);
        Assert.AreEqual(1, service.CreateCalls);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_MissingClaims_ReturnsForbid()
    {
        var service = new FakeModelNoteService();
        var controller = new ModelNotesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"))
                }
            }
        };

        ActionResult<ModelNoteResponse> result = await controller.CreateModelNoteAsync(
            "model-1",
            new PostModelNoteRequest { NoteText = "hello" },
            CancellationToken.None);

        Assert.IsInstanceOfType<ForbidResult>(result.Result);
        Assert.AreEqual(0, service.CreateCalls);
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

    private sealed class FakeModelNoteService : IModelNoteService
    {
        public CreateModelNoteRequest? LastCreateRequest { get; private set; }
        public int CreateCalls { get; private set; }

        public Task<ModelNoteResponse> CreateModelNoteAsync(CreateModelNoteRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;

            LastCreateRequest = new CreateModelNoteRequest
            {
                ModelId = request.ModelId,
                NoteText = request.NoteText.Trim(),
                UserId = request.UserId,
                DisplayName = request.DisplayName,
                UserPrincipalName = request.UserPrincipalName,
                Initials = request.Initials,
                Mentions = request.Mentions
            };

            if (string.IsNullOrWhiteSpace(LastCreateRequest.NoteText))
            {
                throw new ArgumentException("NoteText is required.", nameof(request));
            }

            return Task.FromResult(new ModelNoteResponse
            {
                Id = "note-1",
                ModelId = request.ModelId,
                NoteText = LastCreateRequest.NoteText,
                Author = new ModelNoteAuthorResponse
                {
                    UserId = request.UserId,
                    DisplayName = request.DisplayName,
                    UserPrincipalName = request.UserPrincipalName,
                    Initials = request.Initials
                },
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        public Task<GetModelNotesResponse> GetModelNotesAsync(GetModelNotesRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GetModelNotesResponse
            {
                Notes =
                [
                    new ModelNoteResponse
                    {
                        Id = "note-1",
                        ModelId = request.ModelId,
                        NoteText = "hello",
                        Author = new ModelNoteAuthorResponse
                        {
                            UserId = "u1",
                            DisplayName = "A B",
                            UserPrincipalName = "a@b.com",
                            Initials = "AB"
                        },
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    }
                ]
            });
        }
    }
}
