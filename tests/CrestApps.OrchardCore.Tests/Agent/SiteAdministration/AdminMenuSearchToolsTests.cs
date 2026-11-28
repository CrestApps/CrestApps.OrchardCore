using CrestApps.OrchardCore.AI.Agent.Admin;
using CrestApps.OrchardCore.AI.Agent.Settings;

namespace CrestApps.OrchardCore.Tests.Agent.SiteAdministration;

/// <summary>
/// The tools that read the caller's admin menu find a page by the words of its title and menu path, and list the
/// settings pages from the menu items of the Settings module.
/// </summary>
public sealed class AdminMenuSearchToolsTests
{
    [Fact]
    public async Task FindAdminPage_ReturnsThePageWithItsMenuPathAndLink()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new FindAdminPageTool(), services, new() { ["search"] = "background tasks" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var page = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("Background Tasks", page.GetProperty("title").GetString());
        Assert.Equal("Tools > Background Tasks", page.GetProperty("menuPath").GetString());
        Assert.Equal("/Admin/BackgroundTasks", page.GetProperty("link").GetString());
    }

    [Fact]
    public async Task FindAdminPage_WhenNothingMatches_SaysSo()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new FindAdminPageTool(), services, new() { ["search"] = "url rewriting" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(0, json.GetProperty("total").GetInt32());
        Assert.Contains("No admin page", json.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ListSiteSettingsGroups_ReturnsOnlyTheSettingsPagesOfTheMenu()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSiteSettingsGroupsTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var groups = json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("groupId").GetString()).ToList();

        Assert.Equal(["general", "recaptcha"], groups);
        Assert.Equal("Configuration > Settings > General", json.GetProperty("items")[0].GetProperty("menuPath").GetString());
    }

    [Fact]
    public async Task ListSiteSettingsGroups_FiltersBySearchText()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSiteSettingsGroupsTool(), services, new() { ["search"] = "captcha" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("recaptcha", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("groupId").GetString());
    }
}
