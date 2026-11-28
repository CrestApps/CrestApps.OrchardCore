using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.Tests.Agent.ContentTypes;

/// <summary>
/// The tools that add a field to a content part, change it and remove it.
/// </summary>
public sealed class ContentPartFieldToolsTests
{
    [Fact]
    public async Task SaveContentPartField_ForANewField_AddsItWithAGeneratedNameAndMergesItsSettings()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddType("Article");
        var services = store.BuildServices(new TextField());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContentPartFieldTool(), services, new()
        {
            ["part"] = "Article",
            ["displayName"] = "Sub title",
            ["fieldType"] = "textfield",
            ["settings"] = new JsonObject
            {
                ["TextFieldSettings"] = new JsonObject
                {
                    ["Required"] = true,
                },
            },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("added").GetBoolean());
        Assert.Equal("Subtitle", json.GetProperty("field").GetProperty("name").GetString());
        store.Service.Verify(service => service.AddFieldToPartAsync("Subtitle", "Sub title", "TextField", "Article"), Times.Once);

        var field = Assert.Single(store.PartNamed("Article").Fields);

        Assert.Equal("TextField", field.FieldDefinition.Name);
        Assert.True(field.Settings["TextFieldSettings"]["Required"].GetValue<bool>());
    }

    [Fact]
    public async Task SaveContentPartField_WhenOnlyTheDisplayNameIsGiven_KeepsTheEditorAndDisplayMode()
    {
        // Arrange
        AlterFieldContext altered = null;
        var store = new ContentDefinitionTestStore();
        store.AddPart("Article", part => part.WithField("Subtitle", field => field
            .OfType("TextField")
            .WithDisplayName("Sub title")
            .WithEditor("TextArea")
            .WithDisplayMode("Header")));
        store.Service
            .Setup(service => service.AlterFieldAsync(It.IsAny<AlterFieldContext>()))
            .Callback((AlterFieldContext context) => altered = context)
            .Returns(Task.CompletedTask);
        var services = store.BuildServices(new TextField());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContentPartFieldTool(), services, new()
        {
            ["part"] = "Article",
            ["name"] = "subtitle",
            ["displayName"] = "Strapline",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.NotNull(altered);
        Assert.Equal("Article", altered.PartName);
        Assert.Equal("Subtitle", altered.FieldName);
        Assert.Equal("Strapline", altered.DisplayName);
        Assert.Equal("TextArea", altered.Editor);
        Assert.Equal("Header", altered.DisplayMode);
        store.Service.Verify(service => service.AddFieldToPartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Nope", null, "Sub title", "TextField", "no content part or content type named 'Nope'")]
    [InlineData("Article", null, "Sub title", "NopeField", "no field type named 'NopeField'")]
    [InlineData("Article", null, "Existing", "TextField", "already has a field with the display name 'Existing'")]
    [InlineData("Article", "Existing", null, "NumericField", "cannot be changed after creation")]
    [InlineData("Article", "1st", "First", "TextField", "must start with a letter")]
    public async Task SaveContentPartField_WhenTheArgumentsAreWrong_SaysWhyAndChangesNothing(string part, string name, string displayName, string fieldType, string expected)
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddPart("Article", definition => definition.WithField("Existing", field => field.OfType("TextField").WithDisplayName("Existing")));
        var services = store.BuildServices(new TextField());
        var arguments = new Dictionary<string, object>
        {
            ["part"] = part,
            ["fieldType"] = fieldType,
        };

        if (name is not null)
        {
            arguments["name"] = name;
        }

        if (displayName is not null)
        {
            arguments["displayName"] = displayName;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContentPartFieldTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        store.Service.Verify(service => service.AddFieldToPartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        store.Service.Verify(service => service.AlterFieldAsync(It.IsAny<AlterFieldContext>()), Times.Never);
        store.Manager.Verify(manager => manager.StorePartDefinitionAsync(It.IsAny<ContentPartDefinition>()), Times.Never);
    }

    [Fact]
    public async Task RemoveContentPartField_RemovesTheFieldThroughTheDefinitionService()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddPart("Article", part => part.WithField("Subtitle", field => field.OfType("TextField")));
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveContentPartFieldTool(), services, new()
        {
            ["part"] = "article",
            ["name"] = "subtitle",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("removed").GetBoolean());
        Assert.Equal("TextField", json.GetProperty("fieldType").GetString());
        store.Service.Verify(service => service.RemoveFieldFromPartAsync("Subtitle", "Article"), Times.Once);
    }

    [Fact]
    public async Task RemoveContentPartField_WhenThePartHasNoSuchField_RemovesNothingAndListsTheFields()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddPart("Article", part => part.WithField("Subtitle", field => field.OfType("TextField")));
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveContentPartFieldTool(), services, new()
        {
            ["part"] = "Article",
            ["name"] = "Nope",
        });

        // Assert
        Assert.StartsWith("Nothing was removed", result, StringComparison.Ordinal);
        Assert.Contains("Subtitle", result, StringComparison.Ordinal);
        store.Service.Verify(service => service.RemoveFieldFromPartAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private sealed class TextField : ContentField;
}
