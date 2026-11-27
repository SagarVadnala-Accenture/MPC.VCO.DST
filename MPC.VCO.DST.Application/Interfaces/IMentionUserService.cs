using MPC.VCO.DST.Application.Models;

namespace MPC.VCO.DST.Application.Interfaces;

public interface IMentionUserService
{
    Task<IReadOnlyList<MentionUser>> SearchAsync(
        string search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MentionUser>> GetAuthorizedUsersAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);
}
