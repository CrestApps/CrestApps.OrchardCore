using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Agent.Users;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// The tool that sets custom user settings merges the given fields into the settings stored inside the user,
/// with the two permissions the user editor checks, and saves nothing that is not valid.
/// </summary>
public sealed class SaveUserCustomSettingsToolTests
{
    [Fact]
    public async Task SaveUserCustomSettings_MergesTheGivenFieldsIntoTheStoredSettings()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserCustomSettingsTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["settingsType"] = "userprofile",
            ["settings"] = """{ "ContentItemId": "other", "UserProfile": { "Department": { "Text": "Finance" } } }""",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var stored = harness.Jane.Properties["UserProfile"]["UserProfile"];

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("UserProfile", json.GetProperty("settingsType").GetString());
        Assert.Equal("Finance", json.GetProperty("settings").GetProperty("UserProfile").GetProperty("Department").GetProperty("Text").GetString());
        Assert.Equal("Finance", stored["Department"]["Text"].GetValue<string>());

        // A field that is not given keeps its value.
        Assert.Equal("Paris", stored["Office"]["Text"].GetValue<string>());
        Assert.Equal("user-jane", Assert.Single(harness.Store.Updated));
    }

    [Fact]
    public async Task SaveUserCustomSettings_ForATypeThatIsNotACustomUserSettingsType_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserCustomSettingsTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["settingsType"] = "Article",
            ["settings"] = """{ "TitlePart": { "Title": "Hello" } }""",
        });

        // Assert
        Assert.Equal("Nothing was saved: there is no custom user settings type named 'Article'. The types are: UserProfile.", result);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveUserCustomSettings_WithoutThePermissionOfTheSettingsType_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (permission, _) => permission.Name != "ManageOwnCustomUserSettings_UserProfile",
        };

        using var services = harness.Build();

        var (tool, arguments) = UserRoleToolCalls.Create(SaveUserCustomSettingsTool.TheName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal("Sales", harness.Jane.Properties["UserProfile"]["UserProfile"]["Department"]["Text"].GetValue<string>());
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveUserCustomSettings_WhenTheSettingsAreNotValid_SavesNothing()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            SettingsValidation = new ContentValidateResult(),
        };

        harness.SettingsValidation.Fail(new ValidationResult("The department is not known."));

        using var services = harness.Build();

        var (tool, arguments) = UserRoleToolCalls.Create(SaveUserCustomSettingsTool.TheName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal("Nothing was saved: the settings are not valid. The department is not known.", result);
        Assert.Equal("Sales", harness.Jane.Properties["UserProfile"]["UserProfile"]["Department"]["Text"].GetValue<string>());
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveUserCustomSettings_WhenSettingsIsNotAnObject_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserCustomSettingsTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["settingsType"] = "UserProfile",
            ["settings"] = "Finance",
        });

        // Assert
        Assert.Equal("Nothing was saved: settings must be a JSON object with the parts and fields to change.", result);
        harness.VerifyNothingChanged();
    }
}
