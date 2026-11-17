using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.ContentLocalization;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Agent.PublishLater;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Every Orchard Core content tool acts for the person in the chat: it refuses a caller who is not signed in and a
/// caller who does not hold the permission the matching admin action asks for, and it changes nothing when it refuses.
/// Its JSON schema takes no argument it does not declare and describes each argument.
/// </summary>
public sealed class OrchardContentToolCallerTests
{
    public static TheoryData<string> ToolNames =>
    [
        ListContentItemsTool.TheName,
        ListContentItemVersionsTool.TheName,
        RestoreContentItemVersionTool.TheName,
        DiscardContentItemDraftTool.TheName,
        ChangeContentItemOwnerTool.TheName,
        SaveContentTypePartTool.TheName,
        RemoveContentTypePartTool.TheName,
        SaveContentPartFieldTool.TheName,
        RemoveContentPartFieldTool.TheName,
        ReorderContentTypeElementsTool.TheName,
        ListContentItemLocalizationsTool.TheName,
        LocalizeContentItemTool.TheName,
        ListNestedContentItemsTool.TheName,
        SaveNestedContentItemTool.TheName,
        RemoveNestedContentItemTool.TheName,
        ScheduleContentItemTool.TheName,
    ];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var doubles = new Doubles();
        var services = doubles.BuildServices(signedIn: false, authorized: true);
        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        doubles.VerifyNothingChanged();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermission_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var doubles = new Doubles();
        var services = doubles.BuildServices(signedIn: true, authorized: false);
        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.NotEmpty(services.GetRequiredService<FakeAuthorizationService>().Checks);
        doubles.VerifyNothingChanged();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void JsonSchema_ClosesTheArgumentsAndDescribesEachOne(string toolName)
    {
        // Arrange
        var (tool, _) = CreateTool(toolName);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.Array, schema.GetProperty("required").ValueKind);

        var properties = schema.GetProperty("properties").EnumerateObject().ToList();

        Assert.All(properties, property => Assert.False(string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString())));
        Assert.All(schema.GetProperty("required").EnumerateArray(), required => Assert.Contains(properties, property => property.Name == required.GetString()));
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) CreateTool(string toolName)
    {
        var item = new Dictionary<string, object> { ["contentItemId"] = "item-1" };

        return toolName switch
        {
            ListContentItemsTool.TheName => (new ListContentItemsTool(), []),
            ListContentItemVersionsTool.TheName => (new ListContentItemVersionsTool(), item),
            RestoreContentItemVersionTool.TheName => (new RestoreContentItemVersionTool(), new() { ["contentItemVersionId"] = "version-1" }),
            DiscardContentItemDraftTool.TheName => (new DiscardContentItemDraftTool(), item),
            ChangeContentItemOwnerTool.TheName => (new ChangeContentItemOwnerTool(), new() { ["contentItemId"] = "item-1", ["userName"] = "jane" }),
            SaveContentTypePartTool.TheName => (new SaveContentTypePartTool(), new() { ["contentType"] = "Page", ["part"] = "TitlePart" }),
            RemoveContentTypePartTool.TheName => (new RemoveContentTypePartTool(), new() { ["contentType"] = "Page", ["name"] = "TitlePart" }),
            SaveContentPartFieldTool.TheName => (new SaveContentPartFieldTool(), new() { ["part"] = "Page", ["displayName"] = "Subtitle", ["fieldType"] = "TextField" }),
            RemoveContentPartFieldTool.TheName => (new RemoveContentPartFieldTool(), new() { ["part"] = "Page", ["name"] = "Subtitle" }),
            ReorderContentTypeElementsTool.TheName => (new ReorderContentTypeElementsTool(), new() { ["contentType"] = "Page", ["names"] = new[] { "TitlePart" } }),
            ListContentItemLocalizationsTool.TheName => (new ListContentItemLocalizationsTool(), item),
            LocalizeContentItemTool.TheName => (new LocalizeContentItemTool(), new() { ["contentItemId"] = "item-1", ["targetCulture"] = "fr" }),
            ListNestedContentItemsTool.TheName => (new ListNestedContentItemsTool(), item),
            SaveNestedContentItemTool.TheName => (new SaveNestedContentItemTool(), new() { ["contentItemId"] = "item-1", ["nestedContentItemId"] = "nested-1", ["position"] = 0 }),
            RemoveNestedContentItemTool.TheName => (new RemoveNestedContentItemTool(), new() { ["contentItemId"] = "item-1", ["nestedContentItemId"] = "nested-1" }),
            ScheduleContentItemTool.TheName => (new ScheduleContentItemTool(), new() { ["contentItemId"] = "item-1", ["action"] = "publish", ["when"] = "2999-01-01T08:00" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no test arguments."),
        };
    }

    /// <summary>
    /// The services the tools read before their permission check. Every service that changes the site is strict or
    /// verified, so a refused call that still changed something fails the test.
    /// </summary>
    private sealed class Doubles
    {
        private readonly Mock<IContentManager> _contentManager = new();
        private readonly Mock<IContentDefinitionService> _contentDefinitionService = new(MockBehavior.Strict);
        private readonly Mock<IContentLocalizationManager> _contentLocalizationManager = new(MockBehavior.Strict);
        private readonly Mock<IContentDefinitionManager> _contentDefinitionManager = new();

        public ServiceProvider BuildServices(bool signedIn, bool authorized)
        {
            var contentItem = new ContentItem
            {
                ContentItemId = "item-1",
                ContentItemVersionId = "version-1",
                ContentType = "Page",
                DisplayText = "About",
                Latest = false,
                Published = false,
            };

            var typeDefinition = new ContentTypeDefinitionBuilder()
                .WithName("Page")
                .WithPart("TitlePart")
                .Build();

            _contentManager.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>())).ReturnsAsync(contentItem);
            _contentManager.Setup(manager => manager.GetVersionAsync(It.IsAny<string>())).ReturnsAsync(contentItem);
            _contentManager.Setup(manager => manager.GetAllVersionsAsync(It.IsAny<string>())).ReturnsAsync([contentItem]);

            _contentDefinitionManager.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync([typeDefinition]);
            _contentDefinitionManager.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync(typeDefinition);
            _contentDefinitionManager.Setup(manager => manager.LoadTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync(typeDefinition);

            var services = AgentToolTestContext.CreateServices(signedIn, (_, _) => authorized);

            services.AddSingleton(_contentManager.Object);
            services.AddSingleton(_contentDefinitionManager.Object);
            services.AddSingleton(_contentDefinitionService.Object);
            services.AddSingleton(_contentLocalizationManager.Object);

            return services.BuildServiceProvider();
        }

        public void VerifyNothingChanged()
        {
            _contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.RestoreAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.DiscardDraftAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
            _contentDefinitionManager.Verify(manager => manager.StoreTypeDefinitionAsync(It.IsAny<ContentTypeDefinition>()), Times.Never);
            _contentDefinitionManager.Verify(manager => manager.StorePartDefinitionAsync(It.IsAny<ContentPartDefinition>()), Times.Never);
            _contentDefinitionService.VerifyNoOtherCalls();
            _contentLocalizationManager.VerifyNoOtherCalls();
        }
    }
}
