using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.AI;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.DataConnectors.AI;

/// <summary>
/// The data connector tools check the caller's permission, never return or take a credential, keep a stored credential
/// where it was, never create a SQL Server connection, apply the editors' checks, and cap what a preview returns.
/// </summary>
public sealed class DataConnectorAIToolTests
{
    private const string Canary = "CANARY-SECRET-7f3a";

    private readonly FakeDataConnectionStore _connections = new();
    private readonly FakeConnectedDataSetStore _dataSets = new();
    private readonly FakeRefreshRunStore _runs = new();
    private readonly Mock<IDataSetRowStore> _rows = new();
    private readonly FakeDataConnectorManager _connectors = new FakeDataConnectorManager()
        .Add(DataConnectorConstants.Connectors.HttpApi, DataConnectorCapabilities.Live | DataConnectorCapabilities.Import | DataConnectorCapabilities.IncrementalImport)
        .Add(DataConnectorConstants.Connectors.Elasticsearch, DataConnectorCapabilities.Live | DataConnectorCapabilities.Import | DataConnectorCapabilities.Discovery)
        .Add(DataConnectorConstants.Connectors.MediaFiles, DataConnectorCapabilities.Live | DataConnectorCapabilities.Import | DataConnectorCapabilities.Discovery)
        .Add(DataConnectorConstants.Connectors.SqlServer, DataConnectorCapabilities.Live | DataConnectorCapabilities.Import | DataConnectorCapabilities.IncrementalImport | DataConnectorCapabilities.Discovery);

    private readonly DataConnectionSecretManager _secrets = new(new EphemeralDataProtectionProvider(), NullLogger<DataConnectionSecretManager>.Instance);
    private readonly FakeDataSourceManager _dataSources = new();
    private readonly FixedClock _clock = new();

    public static TheoryData<string, string> ToolsAndPermissions => new()
    {
        { ListDataConnectionsTool.TheName, DataConnectorPermissions.ManageDataConnections.Name },
        { SaveDataConnectionTool.TheName, DataConnectorPermissions.ManageDataConnections.Name },
        { TestDataConnectionTool.TheName, DataConnectorPermissions.ManageDataConnections.Name },
        { DeleteDataConnectionTool.TheName, DataConnectorPermissions.ManageDataConnections.Name },
        { ListDataConnectionResourcesTool.TheName, DataConnectorPermissions.ManageConnectedDataSets.Name },
        { ListConnectedDataSetsTool.TheName, DataConnectorPermissions.ManageConnectedDataSets.Name },
        { GetConnectedDataSetTool.TheName, DataConnectorPermissions.ManageConnectedDataSets.Name },
        { SaveConnectedDataSetTool.TheName, DataConnectorPermissions.ManageConnectedDataSets.Name },
        { RefreshConnectedDataSetTool.TheName, DataConnectorPermissions.RefreshConnectedDataSets.Name },
        { PreviewConnectedDataSetTool.TheName, DataConnectorPermissions.ManageConnectedDataSets.Name },
        { DeleteConnectedDataSetTool.TheName, DataConnectorPermissions.ManageConnectedDataSets.Name },
    };

    public static TheoryData<string> CredentialArguments => new()
    {
        "apiKey",
        "password",
        "bearerToken",
        "clientSecret",
        "headers",
        "ApiKey",
        "Password",
    };

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Arrange
        AddHttpConnection();
        AddDataSet();
        var services = Build(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, ArgumentsFor(toolName));

        // Assert
        Assert.NotNull(permission);
        Assert.Equal(DataConnectorAIToolBase.NotAllowedMessage, result);
        AssertNothingChanged();
    }

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName, string permission)
    {
        // Arrange
        AddHttpConnection();
        AddDataSet();
        var services = Build(authorize: (checkedPermission, _) => checkedPermission.Name != permission);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, ArgumentsFor(toolName));

        // Assert
        Assert.Equal(DataConnectorAIToolBase.NotAllowedMessage, result);
        Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == permission);
        AssertNothingChanged();
    }

    [Fact]
    public async Task ListDataConnections_NeverReturnsAStoredSecretOrHeaderValue_OnlyWhetherOneIsStored()
    {
        // Arrange
        var connection = AddHttpConnection();
        _secrets.Set(connection, HttpAuthenticationSecrets.ApiKey, Canary);
        _secrets.Set(connection, HttpAuthenticationSecrets.Password, Canary);
        _secrets.Set(connection, HttpAuthenticationSecrets.BearerToken, Canary);
        _secrets.Set(connection, HttpAuthenticationSecrets.ClientSecret, Canary);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDataConnectionsTool(), services, new() { ["id"] = connection.ItemId });

        // Assert
        Assert.DoesNotContain(Canary, result, StringComparison.Ordinal);
        var json = AgentToolTestContext.ReadJson(result).GetProperty("connection");
        var credentials = json.GetProperty("credentials");
        Assert.True(credentials.GetProperty("hasApiKey").GetBoolean());
        Assert.True(credentials.GetProperty("hasPassword").GetBoolean());
        Assert.True(credentials.GetProperty("hasBearerToken").GetBoolean());
        Assert.True(credentials.GetProperty("hasClientSecret").GetBoolean());
        Assert.Equal("X-Tenant", json.GetProperty("settings").GetProperty("headerNames")[0].GetString());
        Assert.Equal("https://api.example.com/v1", json.GetProperty("settings").GetProperty("baseUrl").GetString());
    }

    [Fact]
    public async Task ListDataConnections_ReportsASqlServerConnectionReadByName_WithItsSignInAndNoPassword()
    {
        // Arrange
        var connection = new DataConnection
        {
            ItemId = "sql1",
            Name = "Erp",
            DisplayText = "ERP",
            Connector = DataConnectorConstants.Connectors.SqlServer,
        };

        connection.Properties[DataConnectorToolDescriptions.SqlServerSettingsKey] = JsonSerializer.SerializeToNode(new
        {
            Server = "sql.example.com",
            Database = "Erp",
            Authentication = 3,
            UserName = (string)null,
            Encrypt = true,
        });
        _secrets.Set(connection, DataConnectorToolDescriptions.SqlServerPasswordSecret, Canary);
        _connections.Add(connection);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDataConnectionsTool(), services);

        // Assert
        Assert.DoesNotContain(Canary, result, StringComparison.Ordinal);
        var item = AgentToolTestContext.ReadJson(result).GetProperty("items")[0];
        var settings = item.GetProperty("settings");
        Assert.Equal("sql.example.com", settings.GetProperty("server").GetString());
        Assert.Equal("EntraManagedIdentity", settings.GetProperty("authentication").GetString());
        Assert.True(settings.GetProperty("usesHostIdentity").GetBoolean());
        Assert.True(item.GetProperty("credentials").GetProperty("hasPassword").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(CredentialArguments))]
    public async Task SaveDataConnection_RefusesEveryCredentialArgument(string argument)
    {
        // Arrange
        var connection = AddHttpConnection();
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new()
        {
            ["id"] = connection.ItemId,
            [argument] = Canary,
        });

        // Assert
        Assert.StartsWith("Refused:", result, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, result, StringComparison.Ordinal);
        Assert.Empty(_connections.Updated);
        Assert.False(_secrets.Has(connection, HttpAuthenticationSecrets.ApiKey));
    }

    [Fact]
    public async Task SaveDataConnection_RefusesACredentialNestedInAnObject()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new()
        {
            ["connector"] = DataConnectorConstants.Connectors.HttpApi,
            ["displayText"] = "Orders",
            ["authentication"] = JsonElement.Parse($$"""{ "type": "Basic", "password": "{{Canary}}" }"""),
        });

        // Assert
        Assert.StartsWith("Refused:", result, StringComparison.Ordinal);
        Assert.Empty(_connections.Created);
    }

    [Fact]
    public async Task SaveDataConnection_AcceptsTheSettingsWhoseNamesLookLikeSecrets_AndSaysWhichCredentialIsMissing()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new()
        {
            ["connector"] = DataConnectorConstants.Connectors.HttpApi,
            ["displayText"] = "Orders API",
            ["baseUrl"] = "https://orders.example.com",
            ["authenticationType"] = "ApiKey",
            ["apiKeyName"] = "X-Api-Key",
            ["apiKeyPrefix"] = "Key ",
            ["apiKeyLocation"] = "Header",
            ["tokenUrl"] = "https://login.example.com/token",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("API key", json.GetProperty("missingCredentials")[0].GetString());
        var saved = Assert.Single(_connections.Items.Values);
        Assert.Equal("Orders_API", saved.Name);
        var settings = saved.GetOrCreate<HttpConnectionSettings>();
        Assert.Equal("X-Api-Key", settings.Authentication.ApiKeyName);
        Assert.Equal("Key ", settings.Authentication.ApiKeyPrefix);
        Assert.Equal(AgentToolTestContext.UserName, saved.Author);
        Assert.Empty(saved.Secrets);
    }

    [Fact]
    public async Task SaveDataConnection_NeverCreatesASqlServerConnection()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new()
        {
            ["connector"] = DataConnectorConstants.Connectors.SqlServer,
            ["displayText"] = "ERP",
        });

        // Assert
        Assert.StartsWith("Refused:", result, StringComparison.Ordinal);
        Assert.Contains("SEC-A8", result, StringComparison.Ordinal);
        Assert.Empty(_connections.Created);
    }

    [Fact]
    public async Task SaveDataConnection_ChangesOnlyTheNameOfASqlServerConnection()
    {
        // Arrange
        _connections.Add(new DataConnection { ItemId = "sql1", Name = "Erp", DisplayText = "ERP", Connector = DataConnectorConstants.Connectors.SqlServer });
        var services = Build();

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new() { ["id"] = "sql1", ["baseUrl"] = "https://x.example.com" });
        var renamed = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new() { ["id"] = "sql1", ["displayText"] = "ERP (read only)" });

        // Assert
        Assert.StartsWith("Nothing was saved:", refused, StringComparison.Ordinal);
        Assert.True(AgentToolTestContext.ReadJson(renamed).GetProperty("saved").GetBoolean());
        Assert.Equal("ERP (read only)", _connections.Items["sql1"].DisplayText);
    }

    [Fact]
    public async Task SaveDataConnection_RefusesToMoveAStoredCredentialToAnotherServer_ButAllowsAnotherPathOnTheSameServer()
    {
        // Arrange
        var connection = AddHttpConnection();
        _secrets.Set(connection, HttpAuthenticationSecrets.BearerToken, Canary);
        var services = Build();

        // Act
        var moved = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new() { ["id"] = connection.ItemId, ["baseUrl"] = "https://attacker.example.net/v1" });
        var samePlace = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new() { ["id"] = connection.ItemId, ["baseUrl"] = "https://api.example.com/v2" });

        // Assert
        Assert.StartsWith("Refused:", moved, StringComparison.Ordinal);
        Assert.True(AgentToolTestContext.ReadJson(samePlace).GetProperty("saved").GetBoolean());
        Assert.Equal("https://api.example.com/v2", _connections.Items[connection.ItemId].GetOrCreate<HttpConnectionSettings>().BaseUrl);
        Assert.True(_secrets.Has(connection, HttpAuthenticationSecrets.BearerToken));
    }

    [Fact]
    public async Task SaveDataConnection_RefusesAnotherWayOfSigningInWhileACredentialIsStored()
    {
        // Arrange
        var connection = AddHttpConnection();
        _secrets.Set(connection, HttpAuthenticationSecrets.Password, Canary);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new()
        {
            ["id"] = connection.ItemId,
            ["authenticationType"] = "OAuth2",
            ["tokenUrl"] = "https://login.example.com/token",
            ["clientId"] = "reports",
        });

        // Assert
        Assert.StartsWith("Refused:", result, StringComparison.Ordinal);
        Assert.Empty(_connections.Updated);
    }

    [Fact]
    public async Task SaveDataConnection_ReportsTheEditorsChecks_AndSavesNothing()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new()
        {
            ["connector"] = DataConnectorConstants.Connectors.HttpApi,
            ["displayText"] = "Orders",
            ["baseUrl"] = "ftp://orders.example.com",
            ["timeoutSeconds"] = 0,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.False(json.GetProperty("saved").GetBoolean());
        var errors = json.GetProperty("errors").EnumerateArray().Select(error => error.GetString()).ToList();
        Assert.Contains("The URL must be an absolute http:// or https:// address.", errors);
        Assert.Contains("The timeout must be between 1 and 3600 seconds.", errors);
        Assert.Empty(_connections.Created);
    }

    [Fact]
    public async Task SaveDataConnection_RefusesAMediaFolderOutsideTheLibrary()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDataConnectionTool(), services, new()
        {
            ["connector"] = DataConnectorConstants.Connectors.MediaFiles,
            ["displayText"] = "Files",
            ["folder"] = "imports/../../secrets",
        });

        // Assert
        Assert.Contains("The folder can't contain '.' or '..' segments.", result, StringComparison.Ordinal);
        Assert.Empty(_connections.Created);
    }

    [Fact]
    public async Task TestDataConnection_ReturnsTheConnectorsAnswer_OrSaysTheConnectionIsMissing()
    {
        // Arrange
        var connection = AddHttpConnection();
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new TestDataConnectionTool(), services, new() { ["id"] = connection.ItemId });
        var missing = await AgentToolTestContext.InvokeAsync(new TestDataConnectionTool(), services, new() { ["id"] = "nope" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.True(json.GetProperty("succeeded").GetBoolean());
        Assert.Equal("ok", json.GetProperty("message").GetString());
        Assert.Contains("no data connection has the ID 'nope'", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteDataConnection_IsRefusedWhileADataSetReadsIt_ThenDeletes()
    {
        // Arrange
        var connection = AddHttpConnection();
        AddDataSet();
        var services = Build();

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new DeleteDataConnectionTool(), services, new() { ["id"] = connection.ItemId });
        _dataSets.Items.Clear();
        var deleted = await AgentToolTestContext.InvokeAsync(new DeleteDataConnectionTool(), services, new() { ["id"] = connection.ItemId });

        // Assert
        Assert.StartsWith("Nothing was deleted:", refused, StringComparison.Ordinal);
        Assert.Contains("Orders", refused, StringComparison.Ordinal);
        Assert.Equal(connection.ItemId, AgentToolTestContext.ReadJson(deleted).GetProperty("deleted").GetProperty("id").GetString());
        Assert.Equal(new[] { connection.ItemId }, _connections.Deleted);
    }

    [Fact]
    public async Task ListDataConnectionResources_ListsWhatTheConnectionOffers_OrSaysItCannot()
    {
        // Arrange
        var connection = AddHttpConnection(DataConnectorConstants.Connectors.Elasticsearch);
        var http = AddHttpConnection(id: "http2");
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDataConnectionResourcesTool(), services, new() { ["connectionId"] = connection.ItemId });
        var cannot = await AgentToolTestContext.InvokeAsync(new ListDataConnectionResourcesTool(), services, new() { ["connectionId"] = http.ItemId });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.Equal("items", json.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Contains("can't list its resources", cannot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListConnectedDataSets_ReturnsEachDataSetWithItsConnectionAndRefreshState()
    {
        // Arrange
        AddHttpConnection();
        var dataSet = AddDataSet();
        dataSet.Import.RowCount = 42;
        dataSet.Import.LastStatus = DataSetRefreshStatus.Failed;
        dataSet.Import.LastError = "The server answered 500.";
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListConnectedDataSetsTool(), services, new() { ["search"] = "ord" });

        // Assert
        var item = AgentToolTestContext.ReadJson(result).GetProperty("items")[0];
        Assert.Equal("Orders", item.GetProperty("name").GetString());
        Assert.Equal("Orders API", item.GetProperty("connection").GetProperty("displayText").GetString());
        Assert.Equal(42, item.GetProperty("rowCount").GetInt64());
        Assert.Equal("Failed", item.GetProperty("lastStatus").GetString());
    }

    [Fact]
    public async Task GetConnectedDataSet_ReturnsTheLatestRunsWithTheirErrors_AndHidesTheRequestBody()
    {
        // Arrange
        AddHttpConnection();
        var dataSet = AddDataSet();
        dataSet.Put(new CrestApps.OrchardCore.DataConnectors.Connectors.Http.HttpApiDataSetSettings { Method = "POST", Body = Canary });
        _runs.Add(new DataSetRefreshRun { ItemId = "run1", DataSetId = dataSet.ItemId, Status = DataSetRefreshStatus.Failed, Error = "Timed out.", StartedUtc = _clock.UtcNow });
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetConnectedDataSetTool(), services, new() { ["id"] = dataSet.ItemId, ["includeRuns"] = true });
        var missing = await AgentToolTestContext.InvokeAsync(new GetConnectedDataSetTool(), services, new() { ["id"] = "nope" });

        // Assert
        Assert.DoesNotContain(Canary, result, StringComparison.Ordinal);
        var json = AgentToolTestContext.ReadJson(result);
        Assert.True(json.GetProperty("connectorSettings").GetProperty("hasBody").GetBoolean());
        Assert.Equal("Timed out.", json.GetProperty("runs")[0].GetProperty("error").GetString());
        Assert.Equal("ReadConnectedDataSet_Orders", json.GetProperty("readPermission").GetString());
        Assert.Contains("No connected data set has the ID 'nope'", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveConnectedDataSet_CreatesAnImportedDataSetWithTheSourcesFields_AndRequestsItsFirstImport()
    {
        // Arrange
        var connection = AddHttpConnection();
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveConnectedDataSetTool(), services, new()
        {
            ["connectionId"] = connection.ItemId,
            ["displayText"] = "Invoices",
            ["resource"] = "invoices",
            ["accessMode"] = "Imported",
            ["refreshIntervalMinutes"] = 60,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("importRequested").GetBoolean());
        var dataSet = Assert.Single(_dataSets.Items.Values);
        Assert.Equal("Invoices", dataSet.Name);
        Assert.Equal(3, dataSet.Fields.Count);
        Assert.Equal(60, dataSet.Refresh.IntervalMinutes);
        Assert.Equal(_clock.UtcNow, dataSet.Import.RefreshRequestedUtc);
    }

    [Fact]
    public async Task SaveConnectedDataSet_DoesNotStartTheImport_WhenTheUserMayNotRefresh()
    {
        // Arrange
        var connection = AddHttpConnection();
        var services = Build(authorize: (permission, _) => permission.Name != DataConnectorPermissions.RefreshConnectedDataSets.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveConnectedDataSetTool(), services, new()
        {
            ["connectionId"] = connection.ItemId,
            ["displayText"] = "Invoices",
            ["accessMode"] = "Imported",
        });

        // Assert
        Assert.False(AgentToolTestContext.ReadJson(result).GetProperty("importRequested").GetBoolean());
        Assert.Null(Assert.Single(_dataSets.Items.Values).Import.RefreshRequestedUtc);
    }

    [Fact]
    public async Task SaveConnectedDataSet_RefusesARenameAndADuplicateName()
    {
        // Arrange
        var connection = AddHttpConnection();
        var dataSet = AddDataSet();
        var services = Build();

        // Act
        var renamed = await AgentToolTestContext.InvokeAsync(new SaveConnectedDataSetTool(), services, new() { ["id"] = dataSet.ItemId, ["name"] = "Sales" });
        var duplicate = await AgentToolTestContext.InvokeAsync(new SaveConnectedDataSetTool(), services, new() { ["connectionId"] = connection.ItemId, ["displayText"] = "Orders" });

        // Assert
        Assert.Contains("can't be renamed", renamed, StringComparison.Ordinal);
        Assert.Contains("Another data set already has the technical name 'Orders'.", duplicate, StringComparison.Ordinal);
        Assert.Single(_dataSets.Items);
        Assert.Empty(_dataSets.Updated);
    }

    [Fact]
    public async Task RefreshConnectedDataSet_RequestsTheRefreshOfAnImportedDataSet_AndRefusesALiveOne()
    {
        // Arrange
        AddHttpConnection();
        var imported = AddDataSet();
        var live = AddDataSet("live1", "Live", DataSetAccessMode.Live);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RefreshConnectedDataSetTool(), services, new() { ["id"] = imported.ItemId });
        var refused = await AgentToolTestContext.InvokeAsync(new RefreshConnectedDataSetTool(), services, new() { ["id"] = live.ItemId });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("requested").GetBoolean());
        Assert.Equal(_clock.UtcNow, imported.Import.RefreshRequestedUtc);
        Assert.Contains("live data set", refused, StringComparison.Ordinal);
        Assert.Null(live.Import.RefreshRequestedUtc);
    }

    [Fact]
    public async Task PreviewConnectedDataSet_NeedsTheReadPermission_AndReadsAsTheCaller()
    {
        // Arrange
        AddHttpConnection();
        var dataSet = AddDataSet("live1", "Live", DataSetAccessMode.Live);
        _dataSources.Source.Rows.Add([1L, "First"]);
        var refusedServices = Build(authorize: (permission, _) => permission.Name != "ReadConnectedDataSet_Live");
        var services = Build();

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new PreviewConnectedDataSetTool(), refusedServices, new() { ["id"] = dataSet.ItemId });
        var result = await AgentToolTestContext.InvokeAsync(new PreviewConnectedDataSetTool(), services, new() { ["id"] = dataSet.ItemId });

        // Assert
        Assert.Contains("ReadConnectedDataSet_Live", refused, StringComparison.Ordinal);
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("First", json.GetProperty("rows")[0][1].GetString());
        var query = Assert.Single(_dataSources.Source.Queries);
        Assert.Equal("Live", query.DataSet);
        Assert.Equal(AgentToolTestContext.UserName, query.Context.User.Identity.Name);
    }

    [Fact]
    public async Task PreviewConnectedDataSet_CapsTheRowsAndTheirSize()
    {
        // Arrange
        AddHttpConnection();
        var dataSet = AddDataSet("live1", "Live", DataSetAccessMode.Live);

        for (var index = 0; index < 100; index++)
        {
            _dataSources.Source.Rows.Add([(long)index, new string('x', 5_000)]);
        }

        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PreviewConnectedDataSetTool(), services, new() { ["id"] = dataSet.ItemId, ["rows"] = 500 });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal(PreviewConnectedDataSetTool.MaxRows, json.GetProperty("rowCount").GetInt32());
        Assert.True(json.GetProperty("truncated").GetBoolean());
        Assert.True(json.GetProperty("valuesCut").GetBoolean());
        Assert.True(json.GetProperty("rows")[0][1].GetString().Length <= PreviewConnectedDataSetTool.MaxValueLength + 3);
        Assert.True(result.Length < PreviewConnectedDataSetTool.MaxCharacters + 2_000);
    }

    [Fact]
    public async Task PreviewConnectedDataSet_RefusesADisabledDataSet()
    {
        // Arrange
        AddHttpConnection();
        var dataSet = AddDataSet("live1", "Live", DataSetAccessMode.Live);
        dataSet.IsEnabled = false;
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PreviewConnectedDataSetTool(), services, new() { ["id"] = dataSet.ItemId });

        // Assert
        Assert.Contains("is disabled", result, StringComparison.Ordinal);
        Assert.Empty(_dataSources.Source.Queries);
    }

    [Fact]
    public async Task DeleteConnectedDataSet_DeletesItsRowsRunsAndItself_OrSaysItIsMissing()
    {
        // Arrange
        AddHttpConnection();
        var dataSet = AddDataSet();
        var services = Build();

        // Act
        var missing = await AgentToolTestContext.InvokeAsync(new DeleteConnectedDataSetTool(), services, new() { ["id"] = "nope" });
        var result = await AgentToolTestContext.InvokeAsync(new DeleteConnectedDataSetTool(), services, new() { ["id"] = dataSet.ItemId });

        // Assert
        Assert.StartsWith("Nothing was deleted:", missing, StringComparison.Ordinal);
        Assert.Equal(dataSet.ItemId, AgentToolTestContext.ReadJson(result).GetProperty("deleted").GetProperty("id").GetString());
        _rows.Verify(store => store.DeleteAllAsync(dataSet.ItemId, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new[] { (dataSet.ItemId, 0) }, _runs.Pruned);
        Assert.Equal(new[] { dataSet.ItemId }, _dataSets.Deleted);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddLogging();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton<IDataConnectionStore>(_connections);
        services.AddSingleton<IConnectedDataSetStore>(_dataSets);
        services.AddSingleton<IDataSetRefreshRunStore>(_runs);
        services.AddSingleton(_rows.Object);
        services.AddSingleton<IDataConnectorManager>(_connectors);
        services.AddSingleton<IDataConnectionSecretManager>(_secrets);
        services.AddSingleton<IDataSourceManager>(_dataSources);
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton(Options.Create(new DataConnectorsOptions()));
        services.AddSingleton<ConnectedDataReader>();
        services.AddSingleton<DataConnectionEditor>();
        services.AddSingleton<ConnectedDataSetEditor>();
        services.AddSingleton<DataConnectorCommands>();

        return services.BuildServiceProvider();
    }

    private DataConnection AddHttpConnection(string connector = DataConnectorConstants.Connectors.HttpApi, string id = "http1")
    {
        var connection = new DataConnection
        {
            ItemId = id,
            Name = id == "http1" ? "OrdersApi" : "Other_" + id,
            DisplayText = id == "http1" ? "Orders API" : "Other " + id,
            Connector = connector,
            CreatedUtc = _clock.UtcNow,
        };

        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = "https://api.example.com/v1",
            Headers = [new HttpHeaderSetting { Name = "X-Tenant", Value = Canary }],
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.Bearer },
        });

        _connections.Add(connection);

        return connection;
    }

    private ConnectedDataSet AddDataSet(string id = "orders1", string name = "Orders", DataSetAccessMode mode = DataSetAccessMode.Imported)
    {
        var dataSet = new ConnectedDataSet
        {
            ItemId = id,
            Name = name,
            DisplayText = name,
            ConnectionId = "http1",
            Resource = "orders",
            AccessMode = mode,
            Fields =
            [
                new() { Name = "Id", SourceName = "id", DisplayName = "Id", Type = DataFieldType.Integer, SourceType = DataFieldType.Integer, IsIdentifier = true },
                new() { Name = "Name", SourceName = "name", DisplayName = "Name", Type = DataFieldType.Text, SourceType = DataFieldType.Text },
            ],
        };

        _dataSets.Add(dataSet);

        return dataSet;
    }

    private void AssertNothingChanged()
    {
        Assert.Empty(_connections.Created);
        Assert.Empty(_connections.Updated);
        Assert.Empty(_connections.Deleted);
        Assert.Empty(_dataSets.Created);
        Assert.Empty(_dataSets.Updated);
        Assert.Empty(_dataSets.Deleted);
        Assert.Empty(_dataSources.Source.Queries);
    }

    private static Dictionary<string, object> ArgumentsFor(string toolName)
        => toolName switch
        {
            SaveDataConnectionTool.TheName => new() { ["id"] = "http1", ["displayText"] = "Renamed" },
            ListDataConnectionResourcesTool.TheName => new() { ["connectionId"] = "http1" },
            SaveConnectedDataSetTool.TheName => new() { ["id"] = "orders1", ["displayText"] = "Renamed" },
            ListDataConnectionsTool.TheName or ListConnectedDataSetsTool.TheName => [],
            TestDataConnectionTool.TheName or DeleteDataConnectionTool.TheName => new() { ["id"] = "http1" },
            _ => new() { ["id"] = "orders1" },
        };

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListDataConnectionsTool.TheName => new ListDataConnectionsTool(),
            SaveDataConnectionTool.TheName => new SaveDataConnectionTool(),
            TestDataConnectionTool.TheName => new TestDataConnectionTool(),
            DeleteDataConnectionTool.TheName => new DeleteDataConnectionTool(),
            ListDataConnectionResourcesTool.TheName => new ListDataConnectionResourcesTool(),
            ListConnectedDataSetsTool.TheName => new ListConnectedDataSetsTool(),
            GetConnectedDataSetTool.TheName => new GetConnectedDataSetTool(),
            SaveConnectedDataSetTool.TheName => new SaveConnectedDataSetTool(),
            RefreshConnectedDataSetTool.TheName => new RefreshConnectedDataSetTool(),
            PreviewConnectedDataSetTool.TheName => new PreviewConnectedDataSetTool(),
            DeleteConnectedDataSetTool.TheName => new DeleteConnectedDataSetTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName)),
        };
}
