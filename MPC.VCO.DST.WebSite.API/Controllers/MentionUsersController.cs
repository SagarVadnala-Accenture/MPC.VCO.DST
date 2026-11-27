using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace MPC.VCO.DST.WebSite.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users/mentions")]
public sealed class MentionUsersController(
    IMentionUserService mentionUserService)
    : ControllerBase
{
    private readonly IMentionUserService _mentionUserService = mentionUserService;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MentionUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<MentionUserResponse>>> SearchAsync(
        [FromQuery] string search = "",
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MentionUser> users = await _mentionUserService.SearchAsync(search, cancellationToken);

        return Ok(users.Select(user => new MentionUserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email
        }).ToArray());
    }
}
