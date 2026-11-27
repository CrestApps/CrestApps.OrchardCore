using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Settings;
using Moq;
using static CrestApps.OrchardCore.Tests.Agent.SiteAdministration.SiteAdministrationToolHarness;

namespace CrestApps.OrchardCore.Tests.Agent.SiteAdministration;

/// <summary>
/// The site settings tools replay a settings page through the real display manager and MVC model binding: the
/// editor view models are read when the editor is built, a change is merged over the values the editor was built
/// with and posted back through the driver, a driver's validation error stops the save and the reload, and a secret
/// is neither returned nor accepted.
/// </summary>
public sealed class SiteSettingsToolsTests
{
    private const string Section = "ISite.TestSettings";

    [Fact]
    public async Task GetSiteSettings_ReturnsTheEditorValuesAndSchema_WithoutTheSecret()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetSiteSettingsTool(), services, new() { ["groupId"] = GroupId });

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);
        var section = Assert.Single(json.GetProperty("sections").EnumerateArray());
        var values = section.GetProperty("values");

        Assert.Equal(Section, section.GetProperty("name").GetString());
        Assert.Equal("Contoso", values.GetProperty("Title").GetString());
        Assert.Equal(10, values.GetProperty("PageSize").GetInt32());
        Assert.True(values.GetProperty("ApiKey").GetProperty("hasValue").GetBoolean());
        Assert.True(section.GetProperty("schema").GetProperty("properties").GetProperty("ApiKey").GetProperty("readOnly").GetBoolean());
        Assert.Equal("integer", section.GetProperty("schema").GetProperty("properties").GetProperty("PageSize").GetProperty("type").GetString());
        Assert.True(json.GetProperty("canUpdate").GetBoolean());
    }

    [Fact]
    public async Task GetSiteSettings_WhenTheGroupHasNoEditor_SaysItWasNotFound()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetSiteSettingsTool(), services, new() { ["groupId"] = "missing" });

        // Assert
        Assert.StartsWith("No settings page 'missing' was found", result);
    }

    [Fact]
    public async Task UpdateSiteSettings_ChangesOneField_AndKeepsTheOthersAndTheSecret()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = GroupId,
            ["changes"] = JsonNode.Parse($$"""{ "{{Section}}": { "showBanner": true } }"""),
        });

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);
        var stored = harness.StoredSettings[nameof(TestSettings)];

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal(1, harness.SiteSaves);
        Assert.True(stored[nameof(TestSettings.ShowBanner)].GetValue<bool>());
        Assert.Equal("Contoso", stored[nameof(TestSettings.Title)].GetValue<string>());
        Assert.Equal(10, stored[nameof(TestSettings.PageSize)].GetValue<int>());
        Assert.Equal(Canary + "-api-key", stored[nameof(TestSettings.ApiKey)].GetValue<string>());
        Assert.Equal($"{Section}.ShowBanner", Assert.Single(json.GetProperty("changed").EnumerateArray()).GetString());
        harness.ShellReleaseManager.Verify(manager => manager.RequestRelease(), Times.Once());
        harness.ShellReleaseManager.Verify(manager => manager.SuspendReleaseRequest(), Times.Never());
    }

    [Fact]
    public async Task GetSiteSettings_ForAPageWhoseEditorDoesNotShowItsValues_ReadsThemFromTheSavedSettings()
    {
        // Arrange: as Orchard Core's Cultures page, the editor leaves SupportedCultures and DefaultCulture empty.
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetSiteSettingsTool(), services, new() { ["groupId"] = CulturesGroupId });

        // Assert
        var section = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("sections").EnumerateArray());
        var values = section.GetProperty("values");
        var schema = section.GetProperty("schema").GetProperty("properties").GetProperty("SupportedCultures");

        Assert.Equal(new[] { "en-US" }, values.GetProperty("SupportedCultures").EnumerateArray().Select(culture => culture.GetString()));
        Assert.Equal("en-US", values.GetProperty("DefaultCulture").GetString());
        Assert.Equal("array", schema.GetProperty("type").GetString());
        Assert.Contains("JSON text", schema.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSiteSettings_GivenAListForAFieldStoredAsJsonText_PostsItAsJsonText_AndReadsTheSavedValuesBack()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = CulturesGroupId,
            ["changes"] = JsonNode.Parse("""{ "ISite.LocalizationSettings": { "SupportedCultures": ["en-US", "fr-FR"] } }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var stored = harness.StoredSettings[nameof(LocalizationSettings)];
        var values = Assert.Single(json.GetProperty("sections").EnumerateArray()).GetProperty("values");

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal(new[] { "en-US", "fr-FR" }, stored[nameof(LocalizationSettings.SupportedCultures)].AsArray().Select(culture => culture.GetValue<string>()));

        // The default culture wasn't given: the current one was posted back, not cleared.
        Assert.Equal("en-US", stored[nameof(LocalizationSettings.DefaultCulture)].GetValue<string>());
        Assert.Equal("ISite.LocalizationSettings.SupportedCultures", Assert.Single(json.GetProperty("changed").EnumerateArray()).GetString());
        Assert.Equal(new[] { "en-US", "fr-FR" }, values.GetProperty("SupportedCultures").EnumerateArray().Select(culture => culture.GetString()));
    }

    [Fact]
    public async Task UpdateSiteSettings_GivenTextThatIsNotAListForAFieldStoredAsJsonText_SavesNothing()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = CulturesGroupId,
            ["changes"] = JsonNode.Parse("""{ "ISite.LocalizationSettings": { "SupportedCultures": "en-US,fr-FR" } }"""),
        });

        // Assert
        Assert.StartsWith("Nothing was saved: ", result, StringComparison.Ordinal);
        Assert.Contains("takes a list", result, StringComparison.Ordinal);
        Assert.Equal(0, harness.SiteSaves);
    }

    [Fact]
    public async Task UpdateSiteSettings_WhenTheDriverRejectsAValue_ReturnsItsErrorAndSavesNothing()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = GroupId,
            ["changes"] = JsonNode.Parse($$"""{ "{{Section}}": { "PageSize": 0 } }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var error = Assert.Single(json.GetProperty("errors").EnumerateArray());

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal($"{Section}.PageSize", error.GetProperty("field").GetString());
        Assert.Equal("The page size must be greater than zero.", error.GetProperty("message").GetString());
        Assert.Equal(0, harness.SiteSaves);
        harness.ShellReleaseManager.Verify(manager => manager.SuspendReleaseRequest(), Times.Once());
    }

    [Fact]
    public async Task UpdateSiteSettings_WhenARequiredValueIsCleared_ReturnsTheValidationErrorOfTheViewModel()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = GroupId,
            ["changes"] = JsonNode.Parse($$"""{ "{{Section}}": { "Title": "" } }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetProperty("field").GetString() == $"{Section}.Title");
        Assert.Equal(0, harness.SiteSaves);
    }

    [Theory]
    [InlineData("""{ "ISite.TestSettings": { "ApiKey": "new-key" } }""", "is a secret")]
    [InlineData("""{ "ISite.TestSettings": { "Colour": "red" } }""", "is not a field")]
    [InlineData("""{ "ISite.Other": { "Title": "x" } }""", "There is no section")]
    public async Task UpdateSiteSettings_WhenAChangeIsNotAllowed_SavesNothing(string changes, string expected)
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = GroupId,
            ["changes"] = JsonNode.Parse(changes),
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Contains(expected, result);
        Assert.Equal(0, harness.SiteSaves);
        harness.ShellReleaseManager.Verify(manager => manager.RequestRelease(), Times.Never());
    }

    [Fact]
    public async Task UpdateSiteSettings_ChecksTheGroupPermissionWithTheGroupId()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness
        {
            Authorize = (permission, resource) => permission.Name != "ManageResourceSettings" || !Equals(resource, GroupId),
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = GroupId,
            ["changes"] = JsonNode.Parse($$"""{ "{{Section}}": { "ShowBanner": true } }"""),
        });

        // Assert
        Assert.Equal(CrestApps.OrchardCore.AI.Core.AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(0, harness.SiteSaves);
    }

    [Theory]
    [InlineData("Https")]
    [InlineData("userRegistration")]
    [InlineData("userLogin")]
    [InlineData("reverseproxy")]
    public async Task UpdateSiteSettings_ForASettingsPageThatCanLockPeopleOut_SavesNothing(string groupId)
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UpdateSiteSettingsTool(), services, new()
        {
            ["groupId"] = groupId,
            ["changes"] = JsonNode.Parse($$"""{ "{{Section}}": { "ShowBanner": true } }"""),
        });

        // Assert
        Assert.StartsWith("Nothing was saved: the '", result, StringComparison.Ordinal);
        Assert.Contains("are not changed from the chat", result, StringComparison.Ordinal);
        Assert.Equal(0, harness.SiteSaves);
    }
}
