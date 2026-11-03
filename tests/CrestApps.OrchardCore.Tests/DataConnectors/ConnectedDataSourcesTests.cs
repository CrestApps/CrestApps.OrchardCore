using System.Security.Claims;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.DataSources;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// The live and imported data sources list, describe and read connected data sets only for a principal holding the data
/// set's read permission, so importing a data set under the connection's credentials never widens who can read it.
/// </summary>
public sealed class ConnectedDataSourcesTests
{
    [Fact]
    public async Task GetDataSetsAsync_WithNoUser_ListsNothing()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act & Assert
        Assert.Empty(await ListAsync<ImportedDataSource>(harness, null));
        Assert.Empty(await ListAsync<ConnectedDataSource>(harness, null));
    }

    [Fact]
    public async Task GetDataSetsAsync_ListsOnlyTheDataSetsTheUserMayRead()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.CreateReadPermission("Orders").Name);

        // Act
        var imported = await ListAsync<ImportedDataSource>(harness, user);
        var live = await ListAsync<ConnectedDataSource>(harness, user);

        // Assert
        Assert.Equal(["Orders"], imported.Select(descriptor => descriptor.Name));
        Assert.Empty(live);
    }

    [Fact]
    public async Task GetDataSetsAsync_WithReadAll_ListsEveryEnabledDataSetOfItsMode_UnderItsConnection()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);

        // Act
        var imported = await ListAsync<ImportedDataSource>(harness, user);
        var live = await ListAsync<ConnectedDataSource>(harness, user);

        // Assert
        Assert.Equal(["Customers", "Orders"], imported.Select(descriptor => descriptor.Name).Order());
        Assert.Equal(["LiveItems"], live.Select(descriptor => descriptor.Name));
        Assert.All(imported, descriptor => Assert.Equal("Source", descriptor.Group));
    }

    [Fact]
    public async Task ReadAsync_WithoutThePermission_ReadsNoRows_EvenThoughTheRowsAreImported()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.CreateReadPermission("Customers").Name);

        // Act
        var batches = await ReadAsync<ImportedDataSource>(harness, new DataSourceQuery { DataSet = "Orders", Context = new DataSourceContext { User = user } });

        // Assert
        Assert.Empty(batches);
    }

    [Fact]
    public async Task GetSchemaAsync_ForADataSetTheUserMayRead_ListsItsIncludedFields_WithAKeyFilterableIdentifier()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);

        // Act
        DataSetSchema schema = null;
        await harness.InScopeAsync(async provider => schema = await provider.GetRequiredService<ImportedDataSource>().GetSchemaAsync("Orders", new DataSourceContext { User = user }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["Id", "Name", "Modified"], schema.Fields.Select(field => field.Name));
        Assert.True(schema.FindField("Id").IsKeyFilterable);
    }

    [Fact]
    public async Task ReadAsync_ImportedByIdentifier_ReadsOnlyThoseRows()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);
        var query = new DataSourceQuery
        {
            DataSet = "Orders",
            Context = new DataSourceContext { User = user },
            Conditions = [new DataCondition { Field = "Id", Operator = DataFilterOperator.In, Values = ["2", 3L] }],
        };
        query.Fields.Add("Name");

        // Act
        var batches = await ReadAsync<ImportedDataSource>(harness, query);

        // Assert
        var rows = batches.SelectMany(batch => batch.Rows).ToList();
        Assert.Equal(["Grace", "Linus"], rows.Select(row => row[0]));
        Assert.Equal(["Name"], batches[0].Fields.Select(field => field.Name));
    }

    [Fact]
    public async Task ReadAsync_ImportedInSmallBatches_StopsAtTheMostRows()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);

        // Act
        var batches = await ReadAsync<ImportedDataSource>(harness, new DataSourceQuery { DataSet = "Orders", BatchSize = 1, MaxRows = 2, Context = new DataSourceContext { User = user } });

        // Assert
        Assert.Equal([1, 1], batches.Select(batch => batch.Count));
    }

    [Fact]
    public async Task ReadAsync_ADataSetNotImportedYet_YieldsItsFieldsWithNoRows()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);

        // Act
        var batches = await ReadAsync<ImportedDataSource>(harness, new DataSourceQuery { DataSet = "Customers", Context = new DataSourceContext { User = user } });

        // Assert
        var batch = Assert.Single(batches);
        Assert.Equal(0, batch.Count);
        Assert.Equal(3, batch.Fields.Count);
    }

    [Fact]
    public async Task ReadAsync_Live_ReadsThroughTheConnector_AndPassesTheConditionsItCanApply()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);
        harness.Connector.Reads.Clear();

        var query = new DataSourceQuery
        {
            DataSet = "LiveItems",
            Context = new DataSourceContext { User = user },
            Conditions = [new DataCondition { Field = "Id", Operator = DataFilterOperator.GreaterThan, Values = [1L] }],
            MaxRows = 10,
        };

        // Act
        var batches = await ReadAsync<ConnectedDataSource>(harness, query);

        // Assert
        Assert.Equal(3, batches.Sum(batch => batch.Count));
        var read = Assert.Single(harness.Connector.Reads);
        Assert.Equal(DataConnectorReadPurpose.Live, read.Purpose);
        Assert.Equal("id", Assert.Single(read.Conditions).Field);
        Assert.Equal(10, read.MaxRows);
    }

    [Fact]
    public async Task ReadAsync_ADisabledDataSet_ReadsNothing()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var user = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);

        // Act
        var batches = await ReadAsync<ImportedDataSource>(harness, new DataSourceQuery { DataSet = "Disabled", Context = new DataSourceContext { User = user } });

        // Assert
        Assert.Empty(batches);
    }

    private static async Task<DataSetRefreshServiceTests.Harness> CreateAsync()
    {
        var harness = await DataSetRefreshServiceTests.Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1), DataConnectorTestData.Row(2, "Grace", 2), DataConnectorTestData.Row(3, "Linus", 3)];

        var orders = DataConnectorTestData.DataSet(name: "Orders");
        orders.ItemId = "orders";
        await harness.AddDataSetAsync(orders);
        await harness.RefreshAsync(orders);

        var customers = DataConnectorTestData.DataSet(name: "Customers");
        customers.ItemId = "customers";
        await harness.AddDataSetAsync(customers);

        var live = DataConnectorTestData.DataSet(DataSetAccessMode.Live, "LiveItems");
        live.ItemId = "live";
        await harness.AddDataSetAsync(live);

        var disabled = DataConnectorTestData.DataSet(name: "Disabled");
        disabled.ItemId = "disabled";
        disabled.IsEnabled = false;
        await harness.AddDataSetAsync(disabled);

        return harness;
    }

    private static async Task<IReadOnlyList<DataSetDescriptor>> ListAsync<TSource>(DataSetRefreshServiceTests.Harness harness, ClaimsPrincipal user)
        where TSource : IDataSource
    {
        IReadOnlyList<DataSetDescriptor> descriptors = null;

        await harness.InScopeAsync(async provider =>
            descriptors = await provider.GetRequiredService<TSource>().GetDataSetsAsync(new DataSourceContext { User = user }, TestContext.Current.CancellationToken));

        return descriptors;
    }

    private static async Task<List<DataBatch>> ReadAsync<TSource>(DataSetRefreshServiceTests.Harness harness, DataSourceQuery query)
        where TSource : IDataSource
    {
        var batches = new List<DataBatch>();

        await harness.InScopeAsync(async provider =>
        {
            await foreach (var batch in provider.GetRequiredService<TSource>().ReadAsync(query, TestContext.Current.CancellationToken))
            {
                batches.Add(batch);
            }
        });

        return batches;
    }
}
