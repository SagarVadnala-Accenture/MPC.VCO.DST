using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using MPC.VCO.DST.Infrastructure.Configuration;

namespace MPC.VCO.DST.Infrastructure.Entities;

internal sealed class DstDbContext
{
    private readonly CosmosClient _cosmosClient;

    private readonly string _databaseName;
    private readonly string _planningContainerName;
    private readonly string _modelNotesContainerName;

    public DstDbContext(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options)
    {
        _cosmosClient = cosmosClient
            ?? throw new ArgumentNullException(
                nameof(cosmosClient));

        CosmosDbOptions cosmosDbOptions = options?.Value
            ?? throw new ArgumentNullException(
                nameof(options));

        _databaseName =
            cosmosDbOptions.DatabaseName;

        if (string.IsNullOrWhiteSpace(_databaseName))
        {
            throw new InvalidOperationException(
                "CosmosDb:DatabaseName configuration is missing.");
        }

        _planningContainerName =
            cosmosDbOptions.PlanningContainerName;

        if (string.IsNullOrWhiteSpace(_planningContainerName))
        {
            throw new InvalidOperationException(
                "CosmosDb:PlanningContainerName configuration is missing.");
        }

        _modelNotesContainerName =
            cosmosDbOptions.ModelNotesContainerName;

        if (string.IsNullOrWhiteSpace(_modelNotesContainerName))
        {
            throw new InvalidOperationException(
                "CosmosDb:ModelNotesContainerName configuration is missing.");
        }
    }

    /// <summary>
    /// Returns the Cosmos DB container used by the DST application.
    /// </summary>
    public Container PlanningDataContainer =>
        _cosmosClient.GetContainer(
            _databaseName,
            _planningContainerName);

    public Container ModelNotesContainer =>
        _cosmosClient.GetContainer(
            _databaseName,
            _modelNotesContainerName);

    /// <summary>
    /// Gets the configured Cosmos DB database name.
    /// </summary>
    public string DatabaseName => _databaseName;

    /// <summary>
    /// Gets the configured Cosmos DB container name.
    /// </summary>
    public string PlanningContainerName => _planningContainerName;

    /// <summary>
    /// Gets the configured Cosmos DB container name.
    /// </summary>
    public string ModelNotesContainerName => _modelNotesContainerName;
}