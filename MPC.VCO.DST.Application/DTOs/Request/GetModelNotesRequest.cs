namespace MPC.VCO.DST.Application.DTOs.Request;

public sealed class GetModelNotesRequest
{
    public string ModelId { get; init; } = string.Empty;    

    public int PageSize { get; init; } = 20;

    public string? ContinuationToken { get; init; }
}
