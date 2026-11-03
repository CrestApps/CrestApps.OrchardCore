using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.DataConnectors.Deployments;

/// <summary>
/// Exports data connections as a <c>DataConnection</c> recipe step.
/// </summary>
/// <remarks>
/// Secrets are left out: they are protected with this tenant's keys, so no other site could read them. Give them to
/// the importing recipe in clear text under <c>Secrets</c>, or type them in each connection's editor after importing.
/// </remarks>
internal sealed class DataConnectionDeploymentSource : DeploymentSourceBase<DataConnectionDeploymentStep>
{
    private readonly IDataConnectionStore _connectionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectionDeploymentSource"/> class.
    /// </summary>
    /// <param name="connectionStore">The connection store.</param>
    public DataConnectionDeploymentSource(IDataConnectionStore connectionStore)
    {
        _connectionStore = connectionStore;
    }

    protected override async Task ProcessAsync(DataConnectionDeploymentStep step, DeploymentPlanResult result)
    {
        var connections = new JsonArray();

        foreach (var connection in await _connectionStore.ListAsync())
        {
            if (!step.IncludeAll && step.ItemIds?.Contains(connection.ItemId) != true)
            {
                continue;
            }

            connections.Add(JsonSerializer.SerializeToNode(ToEntry(connection), DataConnectorRecipeJson.Options));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [DataConnectionStep.EntriesMemberName] = connections,
        });
    }

    internal static DataConnectionRecipeEntry ToEntry(DataConnection connection)
        => new()
        {
            ItemId = connection.ItemId,
            Name = connection.Name,
            DisplayText = connection.DisplayText,
            Description = connection.Description,
            Connector = connection.Connector,
            Properties = DataConnectorRecipeJson.ExportProperties(connection.Properties),
            CreatedUtc = connection.CreatedUtc,
            ModifiedUtc = connection.ModifiedUtc,
            Author = connection.Author,
        };
}

/// <summary>
/// Exports connected data sets as a <c>ConnectedDataSet</c> recipe step.
/// </summary>
/// <remarks>
/// The rows an imported data set holds and its refresh history stay behind: the importing site imports the rows
/// itself. Each data set names its connection by identifier and by technical name, so export the connections first.
/// </remarks>
internal sealed class ConnectedDataSetDeploymentSource : DeploymentSourceBase<ConnectedDataSetDeploymentStep>
{
    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataConnectionStore _connectionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedDataSetDeploymentSource"/> class.
    /// </summary>
    /// <param name="dataSetStore">The data set store.</param>
    /// <param name="connectionStore">The connection store.</param>
    public ConnectedDataSetDeploymentSource(
        IConnectedDataSetStore dataSetStore,
        IDataConnectionStore connectionStore)
    {
        _dataSetStore = dataSetStore;
        _connectionStore = connectionStore;
    }

    protected override async Task ProcessAsync(ConnectedDataSetDeploymentStep step, DeploymentPlanResult result)
    {
        var connectionNames = (await _connectionStore.ListAsync()).ToDictionary(connection => connection.ItemId, connection => connection.Name, StringComparer.Ordinal);
        var dataSets = new JsonArray();

        foreach (var dataSet in await _dataSetStore.ListAsync())
        {
            if (!step.IncludeAll && step.ItemIds?.Contains(dataSet.ItemId) != true)
            {
                continue;
            }

            connectionNames.TryGetValue(dataSet.ConnectionId ?? string.Empty, out var connectionName);
            dataSets.Add(JsonSerializer.SerializeToNode(ToEntry(dataSet, connectionName), DataConnectorRecipeJson.Options));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [ConnectedDataSetStep.EntriesMemberName] = dataSets,
        });
    }

    internal static ConnectedDataSetRecipeEntry ToEntry(ConnectedDataSet dataSet, string connectionName)
        => new()
        {
            ItemId = dataSet.ItemId,
            Name = dataSet.Name,
            DisplayText = dataSet.DisplayText,
            Description = dataSet.Description,
            ConnectionId = dataSet.ConnectionId,
            ConnectionName = connectionName,
            Resource = dataSet.Resource,
            AccessMode = dataSet.AccessMode,
            IsEnabled = dataSet.IsEnabled,
            Fields = dataSet.Fields,
            DefaultDateField = dataSet.DefaultDateField,
            Refresh = dataSet.Refresh,
            Properties = DataConnectorRecipeJson.ExportProperties(dataSet.Properties),
            CreatedUtc = dataSet.CreatedUtc,
            ModifiedUtc = dataSet.ModifiedUtc,
            Author = dataSet.Author,
        };
}
