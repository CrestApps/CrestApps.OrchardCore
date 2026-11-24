using CrestApps.OrchardCore.AI.Agent.DataPipelines;
using CrestApps.OrchardCore.AI.Agent.SharedLinks;

namespace CrestApps.OrchardCore.Tests.Agent.DataPipelines;

/// <summary>
/// The data pipeline tools list the step types with their ports; the shared link tools list links without their secrets and revoke and delete
/// them through the shared link manager.
/// </summary>
public sealed class DataPipelineToolsTests
{
    [Fact]
    public async Task ListDataPipelineStepTypes_ListsTheStepTypesByCategoryWithTheirPorts()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDataPipelineStepTypesTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var categories = json.GetProperty("categories").EnumerateArray().ToList();

        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(["Source", "Destination"], categories.Select(category => category.GetProperty("category").GetString()));

        var upload = categories[1].GetProperty("stepTypes")[0];
        var input = upload.GetProperty("inputs")[0];

        Assert.Equal("UploadToSftp", upload.GetProperty("name").GetString());
        Assert.Equal("Upload to SFTP", upload.GetProperty("displayName").GetString());
        Assert.Equal("Files", input.GetProperty("kind").GetString());
        Assert.True(input.GetProperty("isRequired").GetBoolean());
        Assert.True(input.GetProperty("allowsMany").GetBoolean());
        Assert.Equal(0, upload.GetProperty("outputs").GetArrayLength());
    }

    [Fact]
    public async Task ListDataPipelineStepTypes_WithACategory_ListsOnlyThatCategory()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDataPipelineStepTypesTool(), services, new() { ["category"] = "Source" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var category = Assert.Single(json.GetProperty("categories").EnumerateArray().ToList());

        Assert.Equal("DataSource", category.GetProperty("stepTypes")[0].GetProperty("name").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ListDataPipelineStepTypes_WithAnUnknownCategory_ExplainsTheChoices()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDataPipelineStepTypesTool(), services, new() { ["category"] = "Loader" });

        // Assert
        Assert.Contains("There is no step category 'Loader'", result);
    }

    [Fact]
    public async Task ListSharedLinks_ListsTheLinksWithTheirStateAndNeverTheirSecrets()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSharedLinksTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal("link-finance", items[0].GetProperty("linkId").GetString());
        Assert.Equal("Pipeline file", items[0].GetProperty("kindName").GetString());
        Assert.Equal("Active", items[0].GetProperty("state").GetString());
        Assert.True(items[0].GetProperty("isActive").GetBoolean());
        Assert.Equal("Dana", items[0].GetProperty("recipients")[0].GetString());
        Assert.Equal(5, items[0].GetProperty("maxUses").GetInt32());
        Assert.Equal("Reports.Share", items[1].GetProperty("kindName").GetString());
        Assert.Equal("Expired", items[1].GetProperty("state").GetString());
        Assert.DoesNotContain("0123456789abcdef", result);
        Assert.DoesNotContain("resource-finance", result);
        Assert.DoesNotContain("user-7", result);
        Assert.False(items[0].TryGetProperty("tokenHash", out _));
    }

    [Fact]
    public async Task ListSharedLinks_WithAQuery_PassesItToTheManager()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSharedLinksTool(), services, new() { ["query"] = "finance" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.False(json.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task RevokeSharedLink_RevokesTheLinkAndReportsIt()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RevokeSharedLinkTool(), services, new() { ["linkId"] = "link-finance" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("revoked").GetBoolean());
        Assert.Equal("Revoked", json.GetProperty("link").GetProperty("state").GetString());
        Assert.Equal(["link-finance"], harness.SharedLinks.Revoked);
        Assert.Empty(harness.SharedLinks.Deleted);
    }

    [Fact]
    public async Task RevokeSharedLink_WhenAlreadyRevoked_ChangesNothing()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        harness.SharedLinks.Links[0].RevokedUtc = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RevokeSharedLinkTool(), services, new() { ["linkId"] = "link-finance" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("revoked").GetBoolean());
        Assert.Empty(harness.SharedLinks.Revoked);
    }

    [Fact]
    public async Task RevokeSharedLink_WhenTheLinkDoesNotExist_SaysSo()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RevokeSharedLinkTool(), services, new() { ["linkId"] = "missing" });

        // Assert
        Assert.Equal("Nothing was revoked: there is no shared link 'missing'. Call listSharedLinks to find its id.", result);
        Assert.Empty(harness.SharedLinks.Revoked);
    }

    [Fact]
    public async Task DeleteSharedLink_DeletesTheLinkThroughTheManager()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSharedLinkTool(), services, new() { ["linkId"] = "link-finance" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("finance-october.xlsx", json.GetProperty("link").GetProperty("title").GetString());
        Assert.Equal(["link-finance"], harness.SharedLinks.Deleted);
        Assert.DoesNotContain(harness.SharedLinks.Links, link => link.LinkId == "link-finance");
    }

    [Fact]
    public async Task DeleteSharedLink_WithoutALinkId_DeletesNothing()
    {
        // Arrange
        var harness = new DataPipelineToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSharedLinkTool(), services, new() { ["linkId"] = " " });

        // Assert
        Assert.StartsWith("Nothing was deleted", result);
        Assert.Empty(harness.SharedLinks.Deleted);
    }
}
