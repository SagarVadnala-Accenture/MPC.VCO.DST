using MPC.VCO.DST.Domain.Models;

namespace MPC.VCO.DST.Domain.Interfaces;

public interface IModelNoteRepository
{
    Task<ModelNoteQueryResult>
        GetByModelIdAsync(
            string modelId,
            int pageSize,
            string? continuationToken,
            CancellationToken cancellationToken = default);

    Task<ModelNote>
        CreateAsync(
            ModelNote note,
            CancellationToken cancellationToken = default);
}
