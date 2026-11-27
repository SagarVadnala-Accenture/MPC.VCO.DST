using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MPC.VCO.DST.WebSite.API.Controllers;

[ApiController]
[Authorize]
[Route("api/models/{modelId}/notes")]
public sealed class ModelNotesController(
    IModelNoteService modelNoteService)
    : ControllerBase
{
    private const string GetModelNotesRouteName = "GetModelNotes";
    private const int MaxMentionCount = 20;

    private readonly IModelNoteService _modelNoteService = modelNoteService;

    [HttpGet(Name = GetModelNotesRouteName)]
    [ProducesResponseType(typeof(IReadOnlyList<ModelNoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ModelNoteResponse>>>
        GetModelNotesAsync(
            [FromRoute] string modelId,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? continuationToken = null,
            CancellationToken cancellationToken = default)
    {
        var request = new GetModelNotesRequest
        {
            ModelId = modelId,
            PageSize = pageSize,
            ContinuationToken = continuationToken
        };

        GetModelNotesResponse response =
            await _modelNoteService.GetModelNotesAsync(
                request,
                cancellationToken);

        if (!string.IsNullOrWhiteSpace(response.ContinuationToken))
        {
            Response.Headers.Append("x-continuation-token", response.ContinuationToken);
        }

        return Ok(response.Notes);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ModelNoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ModelNoteResponse>>
        CreateModelNoteAsync(
            [FromRoute] string modelId,
            [FromBody] PostModelNoteRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthenticatedUser(
                out string userId,
                out string displayName,
                out string userPrincipalName,
                out string initials))
        {
            return Forbid();
        } 

        if (HasMentionCountValidationFailure(request.Mentions, out string? mentionsError))
        {
            ModelState.AddModelError(nameof(PostModelNoteRequest.Mentions), mentionsError);
            return ValidationProblem(ModelState);
        }

        var createRequest = new CreateModelNoteRequest
        {
            ModelId = modelId,
            NoteText = request.NoteText,
            UserId = userId,
            DisplayName = displayName,
            UserPrincipalName = userPrincipalName,
            Initials = initials,
            Mentions = request.Mentions
        };

        ModelNoteResponse response =
            await _modelNoteService.CreateModelNoteAsync(
                createRequest,
                cancellationToken);

        return CreatedAtRoute(
            GetModelNotesRouteName,
            new { modelId },
            response);
    }

    private bool TryGetAuthenticatedUser(
        out string userId,
        out string displayName,
        out string userPrincipalName,
        out string initials)
    {
        userId = string.Empty;
        displayName = string.Empty;
        userPrincipalName = string.Empty;
        initials = string.Empty;

        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        userId = GetClaimValue(User, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier", ClaimTypes.NameIdentifier, "sub") ?? string.Empty;
        displayName = GetClaimValue(User, "name", ClaimTypes.Name) ?? string.Empty;
        userPrincipalName = GetClaimValue(User, "preferred_username", "upn", ClaimTypes.Upn, ClaimTypes.Email) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return false;
        }

        initials = DeriveInitials(displayName);
        return true;
    }

    private static string? GetClaimValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string DeriveInitials(string displayName)
    {
        string[] segments = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return "NA";
        }

        if (segments.Length == 1)
        {
            return segments[0][0].ToString().ToUpperInvariant();
        }

        return string.Concat(segments[0][0], segments[^1][0]).ToUpperInvariant();
    }

    private static bool HasMentionCountValidationFailure(
        IReadOnlyList<NoteMentionRequest>? mentions,
        [NotNullWhen(true)] out string? errorMessage)
    {
        errorMessage = null;

        if (mentions is null || mentions.Count == 0)
        {
            return false;
        }

        int uniqueMentionCount = mentions
            .Select(mention => mention.UserId?.Trim())
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (uniqueMentionCount <= MaxMentionCount)
        {
            return false;
        }

        errorMessage = $"A note cannot mention more than {MaxMentionCount} users.";
        return true;
    }
}

public sealed class PostModelNoteRequest
{
    public string NoteText { get; init; } = string.Empty;

    public IReadOnlyList<NoteMentionRequest> Mentions { get; init; } = [];
}
