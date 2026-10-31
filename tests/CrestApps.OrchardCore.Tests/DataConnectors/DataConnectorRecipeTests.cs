using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Deployments;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Recipes;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using YesSql;
using Harness = CrestApps.OrchardCore.Tests.DataConnectors.DataSetRefreshServiceTests.Harness;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// Exports connections and data sets with their deployment steps, and imports them with their recipe steps, against the
/// YesSql stores on SQLite.
/// </summary>
public sealed class DataConnectorRecipeTests
{
    private static readonly ClaimsPrincipal _reader = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);

    [Fact]
    public async Task DataConnectionStep_CreatesAConnection_AndProtectsItsSecrets()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var step = Step("DataConnection", "Connections", new JsonObject
        {
            ["Name"] = "Warehouse",
            ["DisplayText"] = "Warehouse API",
            ["Connector"] = "Test",
            ["Properties"] = new JsonObject
            {
                [nameof(HttpConnectionSettings)] = new JsonObject
                {
                    ["BaseUrl"] = "https://api.example.com",
                    ["Authentication"] = new JsonObject { ["Type"] = (int)HttpAuthenticationType.Bearer },
                },
            },
            ["Secrets"] = new JsonObject { [HttpAuthenticationSecrets.BearerToken] = "token-1" },
        });

        // Act
        var errors = await RunAsync(harness, step);
        var connection = await FindConnectionAsync(harness, "Warehouse");

        // Assert
        Assert.Empty(errors);
        Assert.Equal("Warehouse API", connection.DisplayText);
        Assert.Equal("Test", connection.Connector);
        Assert.Equal("https://api.example.com", connection.GetOrCreate<HttpConnectionSettings>().BaseUrl);
        Assert.Equal(HttpAuthenticationType.Bearer, connection.GetOrCreate<HttpConnectionSettings>().Authentication.Type);
        Assert.NotEqual("token-1", connection.Secrets[HttpAuthenticationSecrets.BearerToken]);
        Assert.Equal("token-1", await ReadSecretAsync(harness, connection, HttpAuthenticationSecrets.BearerToken));
    }

    [Fact]
    public async Task DataConnectionStep_MatchedByName_KeepsAStoredSecretTheRecipeLeavesEmpty()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await RunAsync(harness, Step("DataConnection", "Connections", new JsonObject
        {
            ["Name"] = "Source",
            ["Secrets"] = new JsonObject { ["Password"] = "first" },
        }));

        // Act
        var errors = await RunAsync(harness, Step("DataConnection", "Connections", new JsonObject
        {
            ["Name"] = "Source",
            ["DisplayText"] = "Renamed",
            ["Secrets"] = new JsonObject { ["Password"] = string.Empty },
        }));
        var connection = await FindConnectionAsync(harness, "Source");

        // Assert
        Assert.Empty(errors);
        Assert.Equal("connection1", connection.ItemId);
        Assert.Equal("Renamed", connection.DisplayText);
        Assert.Equal("first", await ReadSecretAsync(harness, connection, "Password"));
    }

    [Theory]
    [InlineData("Missing", "Test", "isn't enabled")]
    [InlineData("1st", "Test", "technical name")]
    public async Task DataConnectionStep_RejectsAnEntryItCantStore(string name, string connector, string expected)
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var entry = new JsonObject { ["Name"] = name, ["Connector"] = name == "Missing" ? "Nope" : connector };

        // Act
        var errors = await RunAsync(harness, Step("DataConnection", "Connections", entry));

        // Assert
        Assert.Contains(errors, error => error.Contains(expected, StringComparison.Ordinal));
        Assert.Null(await FindConnectionAsync(harness, name));
    }

    [Fact]
    public async Task ConnectedDataSetStep_ResolvesTheConnectionByName_ReadsTheFields_AndQueuesTheFirstImport()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1)];
        var step = Step("ConnectedDataSet", "DataSets", new JsonObject
        {
            ["Name"] = "Orders",
            ["ConnectionId"] = "unknown-on-this-site",
            ["ConnectionName"] = "Source",
            ["Resource"] = "items",
            ["AccessMode"] = "Imported",
            ["Refresh"] = new JsonObject { ["IntervalMinutes"] = 60 },
        });

        // Act
        var errors = await RunAsync(harness, step);
        var dataSet = await FindDataSetAsync(harness, "Orders");
        var refreshed = await harness.RefreshDueAsync();
        var rows = await harness.ReadImportedAsync("Orders", _reader);

        // Assert
        Assert.Empty(errors);
        Assert.Equal("connection1", dataSet.ConnectionId);
        Assert.Equal(["id", "name", "modified"], dataSet.Fields.Select(field => field.SourceName));
        Assert.Equal(harness.Clock.UtcNow, dataSet.Import.RefreshRequestedUtc);
        Assert.Equal(1, refreshed);
        Assert.Equal(["Ada"], rows.Rows.Select(row => row[1]));
    }

    [Fact]
    public async Task ConnectedDataSetStep_RefusesToRenameADataSet()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.AddDataSetAsync(DataConnectorTestData.DataSet());

        // Act
        var errors = await RunAsync(harness, Step("ConnectedDataSet", "DataSets", new JsonObject
        {
            ["ItemId"] = "dataset1",
            ["Name"] = "Other",
        }));

        // Assert
        Assert.Contains(errors, error => error.Contains("can't be renamed", StringComparison.Ordinal));
        Assert.Equal("Items", (await FindDataSetAsync(harness, "Items")).Name);
    }

    [Fact]
    public async Task ConnectedDataSetStep_RejectsAnIncrementalRefreshWithoutAGrowingField()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();

        // Act
        var errors = await RunAsync(harness, Step("ConnectedDataSet", "DataSets", new JsonObject
        {
            ["Name"] = "Orders",
            ["ConnectionName"] = "Source",
            ["AccessMode"] = "Imported",
            ["Fields"] = new JsonArray { new JsonObject { ["SourceName"] = "id", ["Type"] = "Integer", ["IsIdentifier"] = true } },
            ["Refresh"] = new JsonObject { ["Mode"] = "Incremental", ["IncrementalField"] = "missing" },
        }));

        // Assert
        Assert.Contains(errors, error => error.Contains("date or number field", StringComparison.Ordinal));
        Assert.Null(await FindDataSetAsync(harness, "Orders"));
    }

    [Fact]
    public async Task DeploymentSources_ExportWithoutSecretsOrRows_AndTheRecipeStepsImportTheExportOnAnotherSite()
    {
        // Arrange
        await using var source = await Harness.CreateAsync();
        source.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1)];
        await source.InScopeAsync(async provider =>
        {
            var store = provider.GetRequiredService<IDataConnectionStore>();
            var connection = await store.FindByIdAsync("connection1");
            connection.Put(new HttpConnectionSettings { BaseUrl = "https://api.example.com", Authentication = new() { Type = HttpAuthenticationType.OAuth2 } });
            provider.GetRequiredService<IDataConnectionSecretManager>().Set(connection, HttpAuthenticationSecrets.ClientSecret, "s3cret");
            await store.UpdateAsync(connection);
            await provider.GetRequiredService<ISession>().SaveChangesAsync();
        });
        var dataSet = await source.AddDataSetAsync(DataConnectorTestData.DataSet());
        await source.RefreshAsync(dataSet);

        // Act
        var connectionStep = await ExportAsync(source, provider => new DataConnectionDeploymentSource(provider.GetRequiredService<IDataConnectionStore>()), new DataConnectionDeploymentStep());
        var dataSetStep = await ExportAsync(
            source,
            provider => new ConnectedDataSetDeploymentSource(provider.GetRequiredService<IConnectedDataSetStore>(), provider.GetRequiredService<IDataConnectionStore>()),
            new ConnectedDataSetDeploymentStep());

        await using var target = await Harness.CreateAsync();
        await target.InScopeAsync(async provider =>
        {
            var store = provider.GetRequiredService<IDataConnectionStore>();
            await store.DeleteAsync(await store.FindByIdAsync("connection1"));
            await provider.GetRequiredService<ISession>().SaveChangesAsync();
        });
        target.Connector.Rows = [DataConnectorTestData.Row(7, "Imported here", 3)];

        var errors = (await RunAsync(target, connectionStep)).Concat(await RunAsync(target, dataSetStep)).ToList();
        var imported = await FindDataSetAsync(target, "Items");
        var connection = await FindConnectionAsync(target, "Source");
        await target.RefreshDueAsync();
        var rows = await target.ReadImportedAsync("Items", _reader);

        // Assert
        var exportedConnection = Assert.Single(connectionStep["Connections"]!.AsArray())!.AsObject();
        Assert.False(exportedConnection.ContainsKey("Secrets"));
        Assert.DoesNotContain("s3cret", connectionStep.ToJsonString(), StringComparison.Ordinal);

        var exportedDataSet = Assert.Single(dataSetStep["DataSets"]!.AsArray())!.AsObject();
        Assert.Equal("Source", exportedDataSet["ConnectionName"]!.GetValue<string>());
        Assert.Equal("Imported", exportedDataSet["AccessMode"]!.GetValue<string>());
        Assert.False(exportedDataSet.ContainsKey("Import"));

        Assert.Empty(errors);
        Assert.Equal("connection1", connection.ItemId);
        Assert.Equal(HttpAuthenticationType.OAuth2, connection.GetOrCreate<HttpConnectionSettings>().Authentication.Type);
        Assert.False(connection.Secrets.ContainsKey(HttpAuthenticationSecrets.ClientSecret));
        Assert.Equal("dataset1", imported.ItemId);
        Assert.Equal(dataSet.Fields.Select(field => (field.Name, field.Type, field.IsIdentifier)), imported.Fields.Select(field => (field.Name, field.Type, field.IsIdentifier)));
        Assert.Equal(["Imported here"], rows.Rows.Select(row => row[1]));
    }

    [Fact]
    public async Task DeploymentSources_ExportOnlyTheSelectedRecords()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.AddDataSetAsync(DataConnectorTestData.DataSet());
        var other = DataConnectorTestData.DataSet(name: "Others");
        other.ItemId = "dataset2";
        await harness.AddDataSetAsync(other);

        // Act
        var exported = await ExportAsync(
            harness,
            provider => new ConnectedDataSetDeploymentSource(provider.GetRequiredService<IConnectedDataSetStore>(), provider.GetRequiredService<IDataConnectionStore>()),
            new ConnectedDataSetDeploymentStep { IncludeAll = false, ItemIds = ["dataset2"] });

        // Assert
        Assert.Equal(["Others"], exported["DataSets"]!.AsArray().Select(entry => entry!["Name"]!.GetValue<string>()));
    }

    [Fact]
    public async Task RecipeSchemas_AcceptWhatTheDeploymentStepsExport()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.AddDataSetAsync(DataConnectorTestData.DataSet());
        var connectionStep = await ExportAsync(harness, provider => new DataConnectionDeploymentSource(provider.GetRequiredService<IDataConnectionStore>()), new DataConnectionDeploymentStep());
        var dataSetStep = await ExportAsync(
            harness,
            provider => new ConnectedDataSetDeploymentSource(provider.GetRequiredService<IConnectedDataSetStore>(), provider.GetRequiredService<IDataConnectionStore>()),
            new ConnectedDataSetDeploymentStep());

        // Act
        var connectionSchema = await new DataConnectionRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);
        var dataSetSchema = await new ConnectedDataSetRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Evaluate(connectionSchema, connectionStep));
        Assert.True(Evaluate(dataSetSchema, dataSetStep));
        Assert.False(Evaluate(dataSetSchema, new JsonObject { ["name"] = "ConnectedDataSet", ["DataSets"] = new JsonArray { new JsonObject { ["Name"] = "Bad", ["AccessMode"] = "Sometimes" } } }));
    }

    [Fact]
    public void DeploymentSteps_SerializeWithoutTheirCategoryAndTitle()
    {
        // Arrange
        DeploymentStep[] steps = [new DataConnectionDeploymentStep { IncludeAll = false, ItemIds = ["a"] }, new ConnectedDataSetDeploymentStep()];

        // Act
        var json = steps.Select(step => JsonSerializer.SerializeToNode(step, step.GetType())!.AsObject()).ToArray();

        // Assert
        Assert.All(json, step => Assert.False(step.ContainsKey(nameof(DeploymentStep.Category)) || step.ContainsKey(nameof(DeploymentStep.Title))));
        Assert.Equal("a", json[0][nameof(DataConnectionDeploymentStep.ItemIds)]![0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("warehouse-api", "warehouse-api")]
    [InlineData("an identifier far too long for the index", null)]
    [InlineData("has spaces", null)]
    public void NewItemId_KeepsAnIdentifierThatFitsTheIndex_AndMakesOneOtherwise(string itemId, string expected)
    {
        // Act
        var id = DataConnectorRecipeJson.NewItemId(itemId);

        // Assert
        if (expected is null)
        {
            Assert.NotEqual(itemId, id);
            Assert.Equal(26, id.Length);
        }
        else
        {
            Assert.Equal(expected, id);
        }
    }

    private static JsonObject Step(string name, string member, params JsonObject[] entries)
        => new()
        {
            ["name"] = name,
            [member] = new JsonArray([.. entries]),
        };

    private static async Task<List<string>> RunAsync(Harness harness, JsonObject step)
    {
        var context = new RecipeExecutionContext
        {
            Name = step["name"]!.GetValue<string>(),
            Step = step,
        };

        await harness.InScopeAsync(async provider =>
        {
            IRecipeStepHandler handler = context.Name == DataConnectionStep.StepKey
                ? new DataConnectionStep(
                    provider.GetRequiredService<IDataConnectionStore>(),
                    provider.GetRequiredService<IDataConnectorManager>(),
                    provider.GetRequiredService<IDataConnectionSecretManager>(),
                    harness.Clock,
                    new PassThroughStringLocalizer<DataConnectionStep>())
                : new ConnectedDataSetStep(
                    provider.GetRequiredService<IConnectedDataSetStore>(),
                    provider.GetRequiredService<IDataConnectionStore>(),
                    provider.GetRequiredService<IDataConnectorManager>(),
                    provider.GetRequiredService<ConnectedDataReader>(),
                    harness.Clock,
                    NullLogger<ConnectedDataSetStep>.Instance,
                    new PassThroughStringLocalizer<ConnectedDataSetStep>());

            await handler.ExecuteAsync(context);
            await provider.GetRequiredService<ISession>().SaveChangesAsync();
        });

        return [.. context.Errors];
    }

    private static async Task<JsonObject> ExportAsync<TStep>(Harness harness, Func<IServiceProvider, IDeploymentSource> create, TStep step)
        where TStep : DeploymentStep
    {
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await harness.InScopeAsync(provider => create(provider).ProcessDeploymentStepAsync(step, result));

        // The plan is written to a file and read back by the importing site.
        return JsonNode.Parse(Assert.Single(result.Steps).ToJsonString())!.AsObject();
    }

    private static async Task<DataConnection> FindConnectionAsync(Harness harness, string name)
    {
        DataConnection connection = null;

        await harness.InScopeAsync(async provider => connection = await provider.GetRequiredService<IDataConnectionStore>().FindByNameAsync(name, TestContext.Current.CancellationToken));

        return connection;
    }

    private static async Task<ConnectedDataSet> FindDataSetAsync(Harness harness, string name)
    {
        ConnectedDataSet dataSet = null;

        await harness.InScopeAsync(async provider => dataSet = await provider.GetRequiredService<IConnectedDataSetStore>().FindByNameAsync(name, TestContext.Current.CancellationToken));

        return dataSet;
    }

    private static async Task<string> ReadSecretAsync(Harness harness, DataConnection connection, string name)
    {
        string value = null;

        await harness.InScopeAsync(provider =>
        {
            value = provider.GetRequiredService<IDataConnectionSecretManager>().CreateReader(connection).Get(name);

            return Task.CompletedTask;
        });

        return value;
    }

    private static bool Evaluate(JsonSchema schema, JsonObject step)
    {
        using var document = JsonDocument.Parse(step.ToJsonString());

        return schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid;
    }
}
