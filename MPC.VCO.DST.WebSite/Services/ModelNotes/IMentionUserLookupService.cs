using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services.ModelNotes;

public interface IMentionUserLookupService
{
    Task<IReadOnlyList<MentionUser>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
