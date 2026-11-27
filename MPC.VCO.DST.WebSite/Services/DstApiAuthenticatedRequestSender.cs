using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;

namespace MPC.VCO.DST.WebSite.Services;

public sealed class DstApiAuthenticatedRequestSender(
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager)
{
    internal static readonly HttpRequestOptionsKey<ClaimsPrincipal> UserOptionKey =
        new("DstApiAuthenticatedUser");

    private readonly AuthenticationStateProvider _authenticationStateProvider = authenticationStateProvider;
    private readonly NavigationManager _navigationManager = navigationManager;

    public async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken,
        HttpContent? content = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);

        AuthenticationState authenticationState =
            await _authenticationStateProvider.GetAuthenticationStateAsync();
        ClaimsPrincipal user = authenticationState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "An authenticated user is required to call the DST API.");
        }

        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = content
        };
        request.Options.Set(UserOptionKey, user);

        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            string returnUrl = "/" + _navigationManager.ToBaseRelativePath(_navigationManager.Uri);
            string signInUri =
                $"MicrosoftIdentity/Account/SignIn?returnUrl={Uri.EscapeDataString(returnUrl)}";

            _navigationManager.NavigateTo(signInUri, forceLoad: true);
            throw;
        }
    }
}
