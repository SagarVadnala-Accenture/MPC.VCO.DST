using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;


namespace MPC.VCO.DST.Application.Interfaces;

public interface IModelNoteService
{
    Task<GetModelNotesResponse>
        GetModelNotesAsync(
            GetModelNotesRequest request,
            CancellationToken cancellationToken = default);

    Task<ModelNoteResponse>
        CreateModelNoteAsync(
            CreateModelNoteRequest request,
            CancellationToken cancellationToken = default);
}
