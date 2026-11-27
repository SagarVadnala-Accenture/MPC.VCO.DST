using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Models;

namespace MPC.VCO.DST.Application.Implementation;

internal sealed class MentionUserService(IMentionUserDirectory directory) : IMentionUserService
{
    private const int MaxSearchLength = 100;
    private const int MaxResults = 10;

    private readonly IMentionUserDirectory _directory = directory ?? throw new ArgumentNullException(nameof(directory));

    public Task<IReadOnlyList<MentionUser>> SearchAsync(
        string search,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        string normalizedSearch = search.Trim();
        if (normalizedSearch.Length > MaxSearchLength)
        {
            throw new ArgumentOutOfRangeException(nameof(search), $"Search cannot exceed {MaxSearchLength} characters.");
        }

        return _directory.SearchAsync(normalizedSearch, MaxResults, cancellationToken);
    }

    public Task<IReadOnlyList<MentionUser>> GetAuthorizedUsersAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        string[] normalizedIds = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return _directory.GetAuthorizedUsersAsync(normalizedIds, cancellationToken);
    }
}
