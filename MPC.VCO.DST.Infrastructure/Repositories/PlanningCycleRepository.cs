using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Domain.Models;
using MPC.VCO.DST.Infrastructure.Entities;
using MPC.VCO.DST.Infrastructure.Mapper;
using Microsoft.Azure.Cosmos;

namespace MPC.VCO.DST.Infrastructure.Repositories;

internal sealed class PlanningCycleRepository(
    DstDbContext context)
    : IPlanningCycleRepository
{
    private readonly DstDbContext _context = context;

    public async Task<IReadOnlyList<PlanningCycle>>
        GetPlanningCyclesAsync(
            int year,
            int month,
            CancellationToken cancellationToken = default)
    {
        const string query =
            """
            SELECT *
            FROM c
            WHERE c.documentType = "planningCycle"
            AND c.year = @year
            AND c.month = @month
            """;

        var queryDefinition =
            new QueryDefinition(query)
                .WithParameter("@year", year)
                .WithParameter("@month", month);

        FeedIterator<PlanningCycleEntity> iterator =
            _context.PlanningDataContainer
                .GetItemQueryIterator<PlanningCycleEntity>(
                    queryDefinition);

        List<PlanningCycle> results = [];

        while (iterator.HasMoreResults)
        {
            FeedResponse<PlanningCycleEntity> response =
                await iterator.ReadNextAsync(
                    cancellationToken);

            results.AddRange(response.Select(x => x.ToDomain()));
        }

        return results
            .ToList();
    }


    public async Task<PlanningCycle?>
        GetPlanningCycleAsync(
            string planningCycleId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            planningCycleId);

        QueryDefinition query =
            new QueryDefinition(
                """
                SELECT *
                FROM c
                WHERE c.documentType = "planningCycle"
                AND c.planningCycleId = @planningCycleId
                """)
            .WithParameter(
                "@planningCycleId",
                planningCycleId);

        var requestOptions =
            new QueryRequestOptions
            {
                PartitionKey =
                    new PartitionKey(
                        planningCycleId)
            };

        FeedIterator<PlanningCycleEntity> iterator =
            _context.PlanningDataContainer
                .GetItemQueryIterator<PlanningCycleEntity>(
                    query,
                    requestOptions: requestOptions);

        while (iterator.HasMoreResults)
        {
            FeedResponse<PlanningCycleEntity> response =
                await iterator.ReadNextAsync(
                    cancellationToken);

            PlanningCycleEntity? entity =
                response.FirstOrDefault();

            if (entity is not null)
            {
                return entity.ToDomain();
            }
        }

        return null;
    }
}