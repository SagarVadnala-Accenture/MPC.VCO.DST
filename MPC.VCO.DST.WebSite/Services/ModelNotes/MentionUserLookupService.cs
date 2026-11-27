using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services.ModelNotes;

public sealed class MentionUserLookupService(
    ModelsApiClient modelsApiClient)
    : IMentionUserLookupService
{
    private readonly ModelsApiClient _modelsApiClient = modelsApiClient;

    public Task<IReadOnlyList<MentionUser>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return _modelsApiClient.SearchMentionUsersAsync(query, cancellationToken);
    }
}
