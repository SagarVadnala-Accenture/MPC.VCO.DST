using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Identity.Web;

namespace MPC.VCO.DST.WebSite.Services;

public sealed class DstApiAccessTokenHandler : DelegatingHandler
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly string[] _scopes;

    public DstApiAccessTokenHandler(
        ITokenAcquisition tokenAcquisition,
        IConfiguration configuration)
    {
        _tokenAcquisition = tokenAcquisition;

        _scopes =
            configuration.GetSection("DstApi:Scopes").Get<string[]>()
            ?? throw new InvalidOperationException(
                "Configuration key 'DstApi:Scopes' is required.");

        if (_scopes.Length == 0)
        {
            throw new InvalidOperationException(
                "At least one API scope is required in 'DstApi:Scopes'.");
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Options.TryGetValue(
                DstApiAuthenticatedRequestSender.UserOptionKey,
                out ClaimsPrincipal? user)
            || user.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "An authenticated user is required to call the DST API.");
        }
string accessToken =
            await _tokenAcquisition.GetAccessTokenForUserAsync(_scopes, user: user);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
