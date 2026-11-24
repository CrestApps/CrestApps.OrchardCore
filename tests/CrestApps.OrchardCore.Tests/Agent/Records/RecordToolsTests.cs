using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static CrestApps.OrchardCore.Tests.Agent.Records.RecordToolHarness;

namespace CrestApps.OrchardCore.Tests.Agent.Records;

/// <summary>
/// The generic record tools work on any kind a module registers: they check the kind's permissions for the signed-in
/// caller, list and read only the fields the kind allow-lists, write by replaying the record's admin editor through the
/// real display manager and MVC model binding (so the driver's normalization and validation run, then the record's
/// rules), return the field schema with every validation error, check without saving on a dry run, never return or
/// accept a secret, and keep a record that is still in use.
/// </summary>
public sealed class RecordToolsTests
{
    public static TheoryData<string> ToolNames => new()
    {
        ListRecordsTool.TheName,
        GetRecordTool.TheName,
        SaveRecordTool.TheName,
        DeleteRecordTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task EveryTool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var harness = new RecordToolHarness
        {
            SignedIn = false,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName, services), services, CreateArguments(toolName));

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.Records.Created);
        Assert.Empty(harness.Records.Updated);
        Assert.Empty(harness.Records.Deleted);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task EveryTool_RefusesACallerWithoutTheReadPermission(string toolName)
    {
        // Arrange
        var harness = new RecordToolHarness
        {
            Authorize = (permission, _) => permission.Name != ViewRecords.Name,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName, services), services, CreateArguments(toolName));

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.Records.Updated);
        Assert.Empty(harness.Records.Deleted);
    }

    [Theory]
    [InlineData("saveRecord")]
    [InlineData("deleteRecord")]
    public async Task AWriteTool_RefusesACallerWhoCanOnlyRead(string toolName)
    {
        // Arrange
        var harness = new RecordToolHarness
        {
            Authorize = (permission, _) => permission.Name != ManageRecords.Name,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName, services), services, CreateArguments(toolName));

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.Records.Updated);
        Assert.Empty(harness.Records.Deleted);
        Assert.Equal(1, harness.Records.Find("record-1").Limit);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task EveryTool_ForAnUnknownKind_ListsTheKindsTheCallerCanUse(string toolName)
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();
        var arguments = CreateArguments(toolName);

        arguments["kind"] = "invoice";

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName, services), services, arguments);

        // Assert
        Assert.StartsWith("There is no record kind 'invoice'", result);
        Assert.Contains(Kind, result);
    }

    [Fact]
    public async Task ListRecords_ReturnsAPageOfTheAllowListedFields_WithoutSecrets()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListRecordsTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["pageSize"] = 2,
        });

        // Assert
        Assert.DoesNotContain(Canary, result);
        Assert.DoesNotContain("PrivateToken", result);

        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.True(json.GetProperty("truncated").GetBoolean());
        Assert.Equal("Alpha", items[0].GetProperty("name").GetString());
        Assert.True(items[0].GetProperty("hasApiKey").GetBoolean());
        Assert.False(items[0].TryGetProperty("apiKey", out _));
    }

    [Fact]
    public async Task ListRecords_ReturnsTheSecondPage_FilteredByTheQuery()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListRecordsTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["query"] = "a",
            ["page"] = 2,
            ["pageSize"] = 2,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("Gamma", item.GetProperty("name").GetString());
        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("page").GetInt32());
        Assert.False(json.TryGetProperty("truncated", out _));
    }

    [Fact]
    public async Task GetRecord_ReturnsTheAllowListedFields_WithoutSecrets()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-2",
        });

        // Assert
        Assert.DoesNotContain(Canary, result);

        var record = AgentToolTestContext.ReadJson(result).GetProperty("record");

        Assert.Equal("Beta", record.GetProperty("name").GetString());
        Assert.Equal("B", record.GetProperty("code").GetString());
        Assert.True(record.GetProperty("hasApiKey").GetBoolean());
    }

    [Fact]
    public async Task GetRecord_WhenTheRecordDoesNotExist_SaysSo()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "missing",
        });

        // Assert
        Assert.StartsWith("No record of the kind 'testRecord' with the id 'missing' was found", result);
    }

    [Fact]
    public async Task SaveRecord_CreatesARecordThroughTheEditor_WhichNormalizesTheValues()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["values"] = JsonNode.Parse("""{ "Name": "  Delta ", "Code": " d1 ", "Limit": 5 }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var created = Assert.Single(harness.Records.Created);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(created.ItemId, json.GetProperty("id").GetString());
        Assert.Equal("Delta", created.Name);
        Assert.Equal("D1", created.Code);
        Assert.Equal(5, created.Limit);
        Assert.Equal("D1", json.GetProperty("record").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SaveRecord_ChangesOneField_AndKeepsTheOthers_TheSecretAndTheProperties()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-1",
            ["values"] = JsonNode.Parse("""{ "limit": 7 }"""),
        });

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);
        var stored = harness.Records.Find("record-1");

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.Single(harness.Records.Updated);
        Assert.Equal("Limit", Assert.Single(json.GetProperty("changed").EnumerateArray()).GetString());
        Assert.False(json.TryGetProperty("unchanged", out _));
        Assert.Equal(7, stored.Limit);
        Assert.Equal("Alpha", stored.Name);
        Assert.Equal("A", stored.Code);
        Assert.Equal(Canary + "-record-1-key", stored.ApiKey);
        Assert.Equal(Canary + "-record-1-properties", stored.Properties["PrivateToken"]);
    }

    [Fact]
    public async Task SaveRecord_ReportsAFieldTheEditorDidNotChange()
    {
        // Arrange: the editor sets the name only when a record is created, as the catalog editors do.
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-1",
            ["values"] = JsonNode.Parse("""{ "Name": "Omega", "Limit": 2 }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("Limit", Assert.Single(json.GetProperty("changed").EnumerateArray()).GetString());
        Assert.Equal("Name", Assert.Single(json.GetProperty("unchanged").EnumerateArray()).GetString());
        Assert.Equal("Alpha", harness.Records.Find("record-1").Name);
        Assert.Equal(2, harness.Records.Find("record-1").Limit);
    }

    [Fact]
    public async Task SaveRecord_AcceptsValuesKeyedByTheEditorSection()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-2",
            ["values"] = JsonNode.Parse("""{ "TestRecord": { "Code": "b2" } }"""),
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.Equal("B2", harness.Records.Find("record-2").Code);
    }

    [Fact]
    public async Task SaveRecord_OnADryRun_ChecksTheValuesAndSavesNothing()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-1",
            ["values"] = JsonNode.Parse("""{ "Limit": 9 }"""),
            ["dryRun"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("dryRun").GetBoolean());
        Assert.True(json.GetProperty("valid").GetBoolean());
        Assert.Equal(9, json.GetProperty("record").GetProperty("limit").GetInt32());
        Assert.Empty(harness.Records.Updated);
        Assert.Equal(1, harness.Records.Find("record-1").Limit);
    }

    [Fact]
    public async Task SaveRecord_OnADryRunOfAnInvalidRecord_ReturnsTheErrors()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["values"] = JsonNode.Parse("""{ "Name": "Alpha" }"""),
            ["dryRun"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var error = Assert.Single(json.GetProperty("errors").EnumerateArray());

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal("Name", error.GetProperty("field").GetString());
        Assert.Equal("A record with the same name already exists.", error.GetProperty("message").GetString());
        Assert.Empty(harness.Records.Created);
    }

    [Fact]
    public async Task SaveRecord_WhenTheDriverRejectsAValue_ReturnsItsErrorAndTheFieldSchema()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-1",
            ["values"] = JsonNode.Parse("""{ "Limit": -1 }"""),
        });

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);
        var error = Assert.Single(json.GetProperty("errors").EnumerateArray());
        var schema = json.GetProperty("schema");

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal("Limit", error.GetProperty("field").GetString());
        Assert.Equal("The limit cannot be negative.", error.GetProperty("message").GetString());
        Assert.Equal("integer", schema.GetProperty("properties").GetProperty("Limit").GetProperty("type").GetString());
        Assert.True(schema.GetProperty("properties").GetProperty("ApiKey").GetProperty("readOnly").GetBoolean());
        Assert.False(schema.GetProperty("properties").TryGetProperty("IsNew", out _));
        Assert.Empty(harness.Records.Updated);
        Assert.Equal(1, harness.Records.Find("record-1").Limit);
    }

    [Fact]
    public async Task SaveRecord_WhenTheRecordRulesFail_ReturnsTheirErrorAndTheFieldSchema()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["values"] = JsonNode.Parse("""{ "Code": "X" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var error = Assert.Single(json.GetProperty("errors").EnumerateArray());

        Assert.Equal("Name", error.GetProperty("field").GetString());
        Assert.Equal("Name is required.", error.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Object, json.GetProperty("schema").GetProperty("properties").GetProperty("Name").ValueKind);
        Assert.Empty(harness.Records.Created);
    }

    [Theory]
    [InlineData("""{ "ApiKey": "new-key" }""", "is a secret")]
    [InlineData("""{ "Colour": "red" }""", "is not a field")]
    [InlineData("""{ "IsNew": true }""", "cannot be changed")]
    [InlineData("""{ "Limit": { "max": 3 } }""", "takes a single value")]
    public async Task SaveRecord_WhenAValueIsNotAllowed_ReturnsTheSchemaAndSavesNothing(string values, string expected)
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-1",
            ["values"] = JsonNode.Parse(values),
        });

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetProperty("message").GetString().Contains(expected, StringComparison.Ordinal));
        Assert.Equal(JsonValueKind.Object, json.GetProperty("schema").ValueKind);
        Assert.Empty(harness.Records.Updated);
        Assert.Equal(Canary + "-record-1-key", harness.Records.Find("record-1").ApiKey);
    }

    [Fact]
    public async Task SaveRecord_WithoutValues_ReturnsTheFieldSchema()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["values"] = new JsonObject(),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal(JsonValueKind.Object, json.GetProperty("schema").GetProperty("properties").GetProperty("Code").ValueKind);
        Assert.Empty(harness.Records.Created);
    }

    [Fact]
    public async Task SaveRecord_TwiceInOneScope_KeepsTheFirstChange()
    {
        // Arrange: the store hands out the record as it was before the first save, as a cached document does until the
        // scope commits.
        var harness = new RecordToolHarness();
        var original = harness.Records.Find("record-1").Clone();

        using var services = harness.Build();
        var tool = new SaveRecordTool(Kinds(services));

        await AgentToolTestContext.InvokeAsync(tool, services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-1",
            ["values"] = JsonNode.Parse("""{ "Limit": 4 }"""),
        });

        harness.Records.Items[harness.Records.Items.FindIndex(item => item.ItemId == "record-1")] = original;

        // Act
        await AgentToolTestContext.InvokeAsync(tool, services, new()
        {
            ["kind"] = Kind,
            ["id"] = "record-1",
            ["values"] = JsonNode.Parse("""{ "Code": "a2" }"""),
        });

        // Assert
        var stored = harness.Records.Find("record-1");

        Assert.Equal(4, stored.Limit);
        Assert.Equal("A2", stored.Code);
    }

    [Fact]
    public async Task DeleteRecord_DeletesTheRecords_AndKeepsOneThatIsInUse()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = Kind,
            ["ids"] = new[] { "record-1", "record-3", "missing" },
        });

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);
        var deleted = Assert.Single(json.GetProperty("deleted").EnumerateArray());
        var kept = Assert.Single(json.GetProperty("notDeleted").EnumerateArray());

        Assert.Equal("record-1", deleted.GetProperty("id").GetString());
        Assert.Equal("record-3", kept.GetProperty("id").GetString());
        Assert.Equal("It is used by an order.", kept.GetProperty("reason").GetString());
        Assert.Equal("missing", Assert.Single(json.GetProperty("notFound").EnumerateArray()).GetString());
        Assert.Null(harness.Records.Find("record-1"));
        Assert.NotNull(harness.Records.Find("record-3"));
    }

    [Theory]
    [InlineData("saveRecord", "saveTestRecord", "deleteTestRecords", "Records of the kind 'readOnlyRecord' are not saved with saveRecord. Use saveTestRecord: its arguments carry the field names getRecord returns for this kind.")]
    [InlineData("deleteRecord", "saveTestRecord", "deleteTestRecords", "Records of the kind 'readOnlyRecord' are not deleted with deleteRecord. Use deleteTestRecords.")]
    [InlineData("deleteRecord", "saveTestRecord", null, "Records of the kind 'readOnlyRecord' cannot be deleted here; a person does that on the admin page. To change one, use saveTestRecord.")]
    [InlineData("saveRecord", null, "deleteTestRecords", "Records of the kind 'readOnlyRecord' can only be listed and read here; a person changes them on the admin page. To delete one, use deleteTestRecords.")]
    [InlineData("saveRecord", null, null, "Records of the kind 'readOnlyRecord' can only be listed and read here; a person changes them on the admin page.")]
    [InlineData("deleteRecord", null, null, "Records of the kind 'readOnlyRecord' can only be listed and read here; a person changes them on the admin page.")]
    public async Task AWriteTool_RefusesAReadOnlyKind_NamingTheToolThatWritesIt(string toolName, string saveWith, string deleteWith, string expected)
    {
        // Arrange
        var harness = new RecordToolHarness
        {
            Configure = services => AddReadOnlyKind(services, ReadOnlyKind, saveWith, deleteWith),
        };

        using var services = harness.Build();
        var arguments = CreateArguments(toolName);

        arguments["kind"] = ReadOnlyKind;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName, services), services, arguments);

        // Assert
        Assert.Equal(expected, result);
        Assert.Empty(harness.Records.Updated);
        Assert.Empty(harness.Records.Deleted);
        Assert.Equal(1, harness.Records.Find("record-1").Limit);
    }

    [Fact]
    public async Task AReadOnlyKind_IsDescribedWithTheToolsThatWriteIt_AndGetRecordNamesTheSaveTool()
    {
        // Arrange
        var harness = new RecordToolHarness
        {
            Configure = services =>
            {
                AddReadOnlyKind(services, ReadOnlyKind, "saveTestRecord", "deleteTestRecords");
                AddReadOnlyKind(services, "plainReadOnlyRecord", null, null);
            },
        };

        using var services = harness.Build();

        // Act
        var description = new ListRecordsTool(Kinds(services)).JsonSchema
            .GetProperty("properties").GetProperty("kind").GetProperty("description").GetString();
        var readOnlyRecord = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = ReadOnlyKind,
            ["id"] = "record-1",
        }));
        var plainRecord = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetRecordTool(Kinds(services)), services, new()
        {
            ["kind"] = "plainReadOnlyRecord",
            ["id"] = "record-1",
        }));

        // Assert
        Assert.Contains("readOnlyRecord (a read-only record of the tests; read-only here, saved with saveTestRecord and deleted with deleteTestRecords)", description);
        Assert.Contains("plainReadOnlyRecord (a read-only record of the tests)", description);
        Assert.Contains($"{Kind} (a record of the tests)", description);
        Assert.Equal("To change it, call saveTestRecord: its arguments carry these field names.", readOnlyRecord.GetProperty("note").GetString());
        Assert.False(plainRecord.TryGetProperty("note", out var note) && note.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public void ReadOnly_KeepsTheToolNames_AndUseEditorReplayForgetsThem()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddAIRecordKind<TestRecord>("namedTools", kind => kind
            .WithDescription("a record")
            .WithPermission(ManageRecords)
            .WithProjection(record => new { id = record.ItemId })
            .ReadOnly(saveWith: " saveTestRecord ", deleteWith: " "));

        services.AddAIRecordKind<TestRecord>("writableAgain", kind => kind
            .WithDescription("a record")
            .WithPermission(ManageRecords)
            .WithProjection(record => new { id = record.ItemId })
            .ReadOnly(saveWith: "saveTestRecord", deleteWith: "deleteTestRecords")
            .UseEditorReplay());

        using var provider = services.BuildServiceProvider();

        // Act
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;

        // Assert
        Assert.False(kinds["namedTools"].CanWrite);
        Assert.Equal("saveTestRecord", kinds["namedTools"].SaveToolName);
        Assert.Null(kinds["namedTools"].DeleteToolName);
        Assert.True(kinds["writableAgain"].CanWrite);
        Assert.Null(kinds["writableAgain"].SaveToolName);
        Assert.Null(kinds["writableAgain"].DeleteToolName);
    }

    [Fact]
    public void TheKindArgument_IsAnEnumOfTheRegisteredKinds()
    {
        // Arrange
        var harness = new RecordToolHarness();
        using var services = harness.Build();

        // Act
        var schema = new ListRecordsTool(Kinds(services)).JsonSchema;

        // Assert
        var kind = schema.GetProperty("properties").GetProperty("kind");

        Assert.Equal(Kind, Assert.Single(kind.GetProperty("enum").EnumerateArray()).GetString());
        Assert.Contains("a record of the tests", kind.GetProperty("description").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), name => name.GetString() == "kind");
    }

    [Fact]
    public void TheRecordTools_AreRegisteredWithTheirTexts_AndOnlyDeleteRecordAsksForApproval()
    {
        // Arrange
        var services = new ServiceCollection();

        new RecordToolsStartup(new RecordTestLocalizer<RecordToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var tools = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;

        // Assert
        Assert.Equal(4, tools.Count);

        foreach (var name in new[] { ListRecordsTool.TheName, GetRecordTool.TheName, SaveRecordTool.TheName, DeleteRecordTool.TheName })
        {
            var tool = tools[name];

            Assert.NotEqual(name, tool.Title);
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            Assert.NotEqual(name, tool.Description);
            Assert.False(string.IsNullOrWhiteSpace(tool.Category));
            Assert.True(tool.IsSelectable());
            Assert.Equal(name == DeleteRecordTool.TheName, tool.RequiresApproval);
        }
    }

    [Theory]
    [InlineData("TestRecord")]
    [InlineData("test-record")]
    [InlineData("1record")]
    public void AddAIRecordKind_RefusesANameThatIsNotCamelCase(string name)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act and Assert
        Assert.Throws<ArgumentException>(() => services.AddAIRecordKind<TestRecord>(name, kind => kind
            .WithDescription("a record")
            .WithPermission(ManageRecords)
            .WithProjection(record => new { id = record.ItemId })));
    }

    [Fact]
    public void AddAIRecordKind_RefusesAKindWithoutAProjection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => services.AddAIRecordKind<TestRecord>("testRecord", kind => kind
            .WithDescription("a record")
            .WithPermission(ManageRecords)));
    }

    private const string ReadOnlyKind = "readOnlyRecord";

    private static void AddReadOnlyKind(IServiceCollection services, string name, string saveWith, string deleteWith)
        => services.AddAIRecordKind<TestRecord>(name, kind => kind
            .WithTitle("Read-only test record")
            .WithDescription("a read-only record of the tests")
            .WithPermission(ManageRecords)
            .WithReadPermission(ViewRecords)
            .WithProjection(record => new
            {
                id = record.ItemId,
                name = record.Name,
            })
            .ReadOnly(saveWith, deleteWith));

    private static AIFunction CreateTool(string toolName, IServiceProvider services)
        => toolName switch
        {
            ListRecordsTool.TheName => new ListRecordsTool(Kinds(services)),
            GetRecordTool.TheName => new GetRecordTool(Kinds(services)),
            SaveRecordTool.TheName => new SaveRecordTool(Kinds(services)),
            DeleteRecordTool.TheName => new DeleteRecordTool(Kinds(services)),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName)),
        };

    private static Dictionary<string, object> CreateArguments(string toolName)
        => toolName switch
        {
            GetRecordTool.TheName => new()
            {
                ["kind"] = Kind,
                ["id"] = "record-1",
            },
            SaveRecordTool.TheName => new()
            {
                ["kind"] = Kind,
                ["id"] = "record-1",
                ["values"] = JsonNode.Parse("""{ "Limit": 7 }"""),
            },
            DeleteRecordTool.TheName => new()
            {
                ["kind"] = Kind,
                ["ids"] = new[] { "record-1" },
            },
            _ => new()
            {
                ["kind"] = Kind,
            },
        };
}
