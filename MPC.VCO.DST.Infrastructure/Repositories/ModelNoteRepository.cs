using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Domain.Models;
using MPC.VCO.DST.Infrastructure.Entities;
using MPC.VCO.DST.Infrastructure.Mapper;
using Microsoft.Azure.Cosmos;

namespace MPC.VCO.DST.Infrastructure.Repositories;

internal sealed class ModelNoteRepository(
    DstDbContext context)
    : IModelNoteRepository
{
    private readonly DstDbContext _context = context;

    public async Task<ModelNoteQueryResult>
        GetByModelIdAsync(
            string modelId,
            int pageSize,
            string? continuationToken,
            CancellationToken cancellationToken = default)
    {
        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT *
                FROM c
                WHERE c.isDeleted = false
                ORDER BY c.createdAtUtc DESC
                """);

        QueryRequestOptions options = new()
        {
            PartitionKey = new PartitionKey(modelId),
            MaxItemCount = pageSize
        };

        FeedIterator<ModelNoteEntity> iterator =
            _context.ModelNotesContainer
                .GetItemQueryIterator<ModelNoteEntity>(
                    query,
                    continuationToken,
                    options);

        if (!iterator.HasMoreResults)
        {
            return new ModelNoteQueryResult();
        }

        FeedResponse<ModelNoteEntity> response =
            await iterator.ReadNextAsync(cancellationToken);

        return new ModelNoteQueryResult
        {
            Notes = response.Select(entity => entity.ToDomain()).ToArray(),
            ContinuationToken = response.ContinuationToken
        };
    }

    public async Task<ModelNote>
        CreateAsync(
            ModelNote note,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        ModelNoteEntity entity = note.ToEntity();

        ItemResponse<ModelNoteEntity> response =
            await _context.ModelNotesContainer.CreateItemAsync(
                entity,
                new PartitionKey(note.ModelId),
                cancellationToken: cancellationToken);

        return response.Resource.ToDomain();
    }
}
