using System.Net.Http.Headers;
using System.Text.Json;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;

namespace MPC.VCO.DST.Infrastructure.Services;

internal sealed class GraphMentionUserDirectory : IMentionUserDirectory
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0/";
    private readonly HttpClient _httpClient;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<GraphMentionUserDirectory> _logger;
    private readonly string[] _scopes;
    private readonly string _allowedGroupId;

    public GraphMentionUserDirectory(
        HttpClient httpClient,
        ITokenAcquisition tokenAcquisition,
        IConfiguration configuration,
        ILogger<GraphMentionUserDirectory> logger)
    {
        _httpClient = httpClient;
        _tokenAcquisition = tokenAcquisition;
        _logger = logger;
        _scopes = configuration.GetSection("Graph:Scopes").Get<string[]>() ?? [];
        if (_scopes.Length == 0)
        {
            throw new InvalidOperationException("At least one Graph delegated scope must be configured in 'Graph:Scopes'.");
        }

        _allowedGroupId = configuration["MentionUsers:AllowedGroupId"]
            ?? throw new InvalidOperationException("Configuration key 'MentionUsers:AllowedGroupId' is required.");
        if (!Guid.TryParse(_allowedGroupId, out _))
        {
            throw new InvalidOperationException("'MentionUsers:AllowedGroupId' must be a valid Entra group object ID.");
        }
    }

    public async Task<IReadOnlyList<MentionUser>> SearchAsync(
        string search,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string filter = string.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                string escapedSearch = search.Replace("'", "''", StringComparison.Ordinal);
                filter = "$filter=" + Uri.EscapeDataString(
                    $"startswith(displayName,'{escapedSearch}') or startswith(mail,'{escapedSearch}') or startswith(userPrincipalName,'{escapedSearch}')") + "&";
            }

            string url = $"groups/{_allowedGroupId}/transitiveMembers/microsoft.graph.user?{filter}$select=id,displayName,mail,userPrincipalName&$top={maxResults}&$count=true";
            using HttpRequestMessage request = new(HttpMethod.Get, new Uri(new Uri(GraphBaseUrl), url));
            request.Headers.Add("ConsistencyLevel", "eventual");
            using HttpResponseMessage response = await SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return ReadUsers(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Graph mention-user search was canceled for group {AllowedGroupId}.",
                _allowedGroupId);
            throw;
        }
        catch (MicrosoftIdentityWebChallengeUserException exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to acquire Graph access token while searching mention users for group {AllowedGroupId}.",
                _allowedGroupId);
            throw;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to query Graph mention users for group {AllowedGroupId}.",
                _allowedGroupId);
            throw;
        }
    }

    public async Task<IReadOnlyList<MentionUser>> GetAuthorizedUsersAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        try
        {
            string idFilter = string.Join(" or ", userIds.Select(id => $"id eq '{id.Replace("'", "''", StringComparison.Ordinal)}'"));
            string url = $"groups/{_allowedGroupId}/transitiveMembers/microsoft.graph.user?$filter={Uri.EscapeDataString(idFilter)}&$select=id,displayName,mail,userPrincipalName&$top=20&$count=true";
            using HttpRequestMessage request = new(HttpMethod.Get, new Uri(new Uri(GraphBaseUrl), url));
            request.Headers.Add("ConsistencyLevel", "eventual");
            using HttpResponseMessage response = await SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return ReadUsers(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Graph authorized-user lookup was canceled for group {AllowedGroupId}.",
                _allowedGroupId);
            throw;
        }
        catch (MicrosoftIdentityWebChallengeUserException exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to acquire Graph access token while resolving authorized users for group {AllowedGroupId}.",
                _allowedGroupId);
            throw;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to query Graph authorized users for group {AllowedGroupId}.",
                _allowedGroupId);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string token = await _tokenAcquisition.GetAccessTokenForUserAsync(_scopes);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static IReadOnlyList<MentionUser> ReadUsers(JsonElement root)
    {
        if (!root.TryGetProperty("value", out JsonElement users) || users.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Microsoft Graph returned an invalid user collection response.");
        }

        List<MentionUser> results = [];
        foreach (JsonElement user in users.EnumerateArray())
        {
            string id = GetString(user, "id");
            string displayName = GetString(user, "displayName");
            string email = GetString(user, "mail");
            if (string.IsNullOrWhiteSpace(email))
            {
                email = GetString(user, "userPrincipalName");
            }

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(displayName))
            {
                results.Add(new MentionUser(id, displayName, email));
            }
        }

        return results;
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
