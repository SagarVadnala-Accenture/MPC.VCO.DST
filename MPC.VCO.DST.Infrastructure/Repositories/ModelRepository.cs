using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Domain.Models;
using MPC.VCO.DST.Infrastructure.Entities;
using MPC.VCO.DST.Infrastructure.Mapper;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace MPC.VCO.DST.Infrastructure.Repositories;

internal sealed class ModelRepository(
    DstDbContext context,
    ILogger<ModelRepository> logger)
    : IModelRepository
{
    private readonly DstDbContext _context = context;
    private readonly ILogger<ModelRepository> _logger = logger;
    private const int MaxCounterReservationAttempts = 5;

    public async Task<string>
        ReserveNextModelIdAsync(
            string planningCycleId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planningCycleId);

        string counterId = GetCounterId(planningCycleId);
        PartitionKey partitionKey = new(planningCycleId);

        for (int attempt = 0; attempt < MaxCounterReservationAttempts; attempt++)
        {
            try
            {
                ItemResponse<ModelCounterEntity> readResponse =
                    await _context.PlanningDataContainer.ReadItemAsync<ModelCounterEntity>(
                        counterId,
                        partitionKey,
                        cancellationToken: cancellationToken);

                ModelCounterEntity counter = readResponse.Resource;
                int nextSequence = counter.LastAllocatedSequence + 1;

                await _context.PlanningDataContainer.PatchItemAsync<ModelCounterEntity>(
                    counterId,
                    partitionKey,
                    [
                        PatchOperation.Increment("/lastAllocatedSequence", 1)
                    ],
                    new PatchItemRequestOptions
                    {
                        IfMatchEtag = readResponse.ETag
                    },
                    cancellationToken);

                return FormatModelId(planningCycleId, nextSequence);
            }
            catch (CosmosException exception)
                when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug(
                    exception,
                    "Model counter not found for planning cycle {PlanningCycleId}; attempting counter bootstrap.",
                    planningCycleId);

                int highestExistingSequence =
                    await GetHighestExistingSequenceAsync(
                        planningCycleId,
                        cancellationToken);

                int nextSequence = highestExistingSequence + 1;

                try
                {
                    ModelCounterEntity newCounter = new()
                    {
                        Id = counterId,
                        DocumentType = "modelCounter",
                        PlanningCycleId = planningCycleId,
                        LastAllocatedSequence = nextSequence
                    };

                    await _context.PlanningDataContainer.CreateItemAsync(
                        newCounter,
                        partitionKey,
                        cancellationToken: cancellationToken);

                    return FormatModelId(planningCycleId, nextSequence);
                }
                catch (CosmosException createException)
                    when (createException.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogDebug(
                        createException,
                        "Model counter bootstrap conflict for planning cycle {PlanningCycleId}; retrying reservation attempt {Attempt}.",
                        planningCycleId,
                        attempt + 1);

                    // Another request created the counter first; retry read/update flow.
                }
            }
            catch (CosmosException exception)
                when (exception.StatusCode == System.Net.HttpStatusCode.PreconditionFailed
                    || exception.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogDebug(
                    exception,
                    "Model counter concurrent update for planning cycle {PlanningCycleId}; retrying reservation attempt {Attempt}.",
                    planningCycleId,
                    attempt + 1);

                // Counter was updated by another request between read and patch; retry.
            }
        }

        throw new InvalidOperationException(
            "Could not reserve a model id due to concurrent updates. Please retry the request.");
    }

    public async Task<IReadOnlyList<DstModel>>
        GetByPlanningCycleAsync(
            string planningCycleId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        int offset = (pageNumber - 1) * pageSize;

        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT *
                FROM c
                WHERE c.documentType = "model"
                AND c.planningCycleId = @planningCycleId
                ORDER BY c.createdOn DESC
                OFFSET @offset LIMIT @pageSize
                """)
            .WithParameter(
                "@planningCycleId",
                planningCycleId)
            .WithParameter(
                "@offset",
                offset)
            .WithParameter(
                "@pageSize",
                pageSize);

        FeedIterator<ModelEntity> iterator =
            _context.PlanningDataContainer
                .GetItemQueryIterator<ModelEntity>(
                    query,
                    requestOptions:
                    new QueryRequestOptions
                    {
                        PartitionKey =
                            new PartitionKey(
                                planningCycleId)
                    });

        List<DstModel> models = [];

        while (iterator.HasMoreResults)
        {
            FeedResponse<ModelEntity> response =
                await iterator.ReadNextAsync(
                    cancellationToken);

            models.AddRange(
                response.Select(x => x.ToDomain()));
        }

        return models;
    }

    public async Task<IReadOnlyList<DstModel>>
        GetByPlanningCycleAsync(
            string planningCycleId,
            CancellationToken cancellationToken = default)
    {
        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT *
                FROM c
                WHERE c.documentType = "model"
                AND c.planningCycleId = @planningCycleId
                ORDER BY c.createdOn DESC
                """)
            .WithParameter(
                "@planningCycleId",
                planningCycleId);

        FeedIterator<ModelEntity> iterator =
            _context.PlanningDataContainer
                .GetItemQueryIterator<ModelEntity>(
                    query,
                    requestOptions:
                    new QueryRequestOptions
                    {
                        PartitionKey =
                            new PartitionKey(
                                planningCycleId)
                    });

        List<DstModel> models = [];

        while (iterator.HasMoreResults)
        {
            FeedResponse<ModelEntity> response =
                await iterator.ReadNextAsync(
                    cancellationToken);

            models.AddRange(
                response.Select(x => x.ToDomain()));
        }

        return models;
    }

    public async Task<DstModel?>
        GetByIdAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default)
    {
        ModelEntity? entity =
            await GetEntityByModelIdAsync(
                planningCycleId,
                modelId,
                cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<DstModel>
        CreateAsync(
            DstModel model,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        ModelEntity entity =
            model.ToEntity(model.ModelId);

        ItemResponse<ModelEntity> response =
            await _context.PlanningDataContainer
                .CreateItemAsync(
                    entity,
                    new PartitionKey(model.PlanningCycleId),
                    cancellationToken: cancellationToken);

        return response.Resource.ToDomain();
    }

    public async Task<DstModel?>
        UpdateAsync(
            DstModel model,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        ModelEntity entity =
            model.ToEntity(model.ModelId);

        try
        {
            ItemResponse<ModelEntity> response =
                await _context.PlanningDataContainer
                    .ReplaceItemAsync(
                        entity,
                        model.ModelId,
                        new PartitionKey(model.PlanningCycleId),
                        cancellationToken: cancellationToken);

            return response.Resource.ToDomain();
        }
        catch (CosmosException exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool>
        DeleteAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default)
    {
        string? documentId =
            await GetEntityIdByModelIdAsync(
                planningCycleId,
                modelId,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return false;
        }

        await _context.PlanningDataContainer
            .DeleteItemAsync<ModelEntity>(
                documentId,
                new PartitionKey(planningCycleId),
                cancellationToken: cancellationToken);

        return true;
    }

    private async Task<ModelEntity?>
        GetEntityByModelIdAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken)
    {
        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT TOP 1 *
                FROM c
                WHERE c.documentType = "model"
                AND c.modelId = @modelId
                """)
            .WithParameter(
                "@modelId",
                modelId);

        FeedIterator<ModelEntity> iterator =
            _context.PlanningDataContainer
                .GetItemQueryIterator<ModelEntity>(
                    query,
                    requestOptions:
                    new QueryRequestOptions
                    {
                        PartitionKey =
                            new PartitionKey(
                                planningCycleId)
                    });

        while (iterator.HasMoreResults)
        {
            FeedResponse<ModelEntity> response =
                await iterator.ReadNextAsync(
                    cancellationToken);

            ModelEntity? entity =
                response.FirstOrDefault();

            if (entity != null)
                return entity;
        }

        return null;
    }

    private async Task<string?>
        GetEntityIdByModelIdAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken)
    {
        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT TOP 1 c.id
                FROM c
                WHERE c.documentType = "model"
                AND c.modelId = @modelId
                """)
            .WithParameter(
                "@modelId",
                modelId);

        FeedIterator<ModelDocumentIdProjection> iterator =
            _context.PlanningDataContainer
                .GetItemQueryIterator<ModelDocumentIdProjection>(
                    query,
                    requestOptions:
                    new QueryRequestOptions
                    {
                        PartitionKey =
                            new PartitionKey(
                                planningCycleId)
                    });

        while (iterator.HasMoreResults)
        {
            FeedResponse<ModelDocumentIdProjection> response =
                await iterator.ReadNextAsync(
                    cancellationToken);

            string? documentId =
                response
                    .FirstOrDefault()
                    ?.Id;

            if (!string.IsNullOrWhiteSpace(documentId))
            {
                return documentId;
            }
        }

        return null;
    }

    public async Task<DstModel?>
        GetHighestExpectedValueModelAsync(
            string planningCycleId,
            CancellationToken cancellationToken = default)
    {
        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT TOP 1 *
                FROM c
                WHERE c.documentType = "model"
                AND c.planningCycleId = @planningCycleId
                ORDER BY c.expectedValueMonthlyUsd DESC
                """)
            .WithParameter(
                "@planningCycleId",
                planningCycleId);

        FeedIterator<ModelEntity> iterator =
            _context.PlanningDataContainer
                .GetItemQueryIterator<ModelEntity>(
                    query,
                    requestOptions:
                    new QueryRequestOptions
                    {
                        PartitionKey =
                            new PartitionKey(
                                planningCycleId)
                    });

        while (iterator.HasMoreResults)
        {
            FeedResponse<ModelEntity> response =
                await iterator.ReadNextAsync(
                    cancellationToken);

            ModelEntity? entity =
                response.FirstOrDefault();

            if (entity != null)
                return entity.ToDomain();
        }

        return null;
    }

    private async Task<int>
        GetHighestExistingSequenceAsync(
            string planningCycleId,
            CancellationToken cancellationToken)
    {
        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT c.modelId
                FROM c
                WHERE c.documentType = "model"
                AND c.planningCycleId = @planningCycleId
                """)
            .WithParameter(
                "@planningCycleId",
                planningCycleId);

        FeedIterator<ModelIdProjection> iterator =
            _context.PlanningDataContainer.GetItemQueryIterator<ModelIdProjection>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(planningCycleId)
                });

        int highestSequence = 0;

        while (iterator.HasMoreResults)
        {
            FeedResponse<ModelIdProjection> response =
                await iterator.ReadNextAsync(cancellationToken);

            foreach (ModelIdProjection item in response)
            {
                int? parsedSequence = ParseSequenceNumber(
                    planningCycleId,
                    item.ModelId);

                if (parsedSequence.HasValue)
                {
                    highestSequence = Math.Max(
                        highestSequence,
                        parsedSequence.Value);
                }
            }
        }

        return highestSequence;
    }

    private static string GetCounterId(string planningCycleId)
        => $"{planningCycleId}-model-counter";

    private static string FormatModelId(string planningCycleId, int sequence)
        => $"{planningCycleId}-{sequence:000}";

    private static int? ParseSequenceNumber(
        string planningCycleId,
        string modelId)
    {
        if (!modelId.StartsWith(
                planningCycleId + "-",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string suffix = modelId[(planningCycleId.Length + 1)..];

        return int.TryParse(suffix, out int sequence)
            ? sequence
            : null;
    }

    private sealed class ModelIdProjection
    {
        [System.Text.Json.Serialization.JsonPropertyName("modelId")]
        public string ModelId { get; init; } = string.Empty;
    }

    private sealed class ModelDocumentIdProjection
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
    }
}