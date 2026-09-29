using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The contact's activity pages show the list part's header and bar when the contact type has the ListPart, read from
/// the list part's settings: whether to show the header, and which content types the list's New button offers.
/// </summary>
public sealed class ContactListPartNavigationResolverTests
{
    [Fact]
    public async Task ResolveAsync_AContactWithoutAList_HasNoListNavigation()
    {
        // Arrange
        var resolver = CreateResolver(CreateContactType(listPartSettings: null));

        // Act
        var navigation = await resolver.ResolveAsync(CreateContact());

        // Assert
        Assert.Null(navigation);
    }

    [Fact]
    public async Task ResolveAsync_AContactThatIsAList_ReadsTheHeaderSettingAndTheContainedTypes()
    {
        // Arrange
        var contactType = CreateContactType(new JsonObject
        {
            ["ContainedContentTypes"] = new JsonArray("Plan", "Removed", "LeadGeneration"),
            ["ShowHeader"] = true,
            ["EnableOrdering"] = true,
        });

        var resolver = CreateResolver(
            contactType,
            new ContentTypeDefinition("Plan", "Plan"),
            new ContentTypeDefinition("LeadGeneration", "Lead Generation"));

        // Act
        var navigation = await resolver.ResolveAsync(CreateContact());

        // Assert
        Assert.NotNull(navigation);
        Assert.Same(contactType, navigation.ContactTypeDefinition);
        Assert.True(navigation.ShowHeader);
        Assert.True(navigation.EnableOrdering);

        // A contained type that no longer exists is left out of the New button.
        Assert.Equal(["Plan", "LeadGeneration"], navigation.ContainedContentTypeDefinitions.Select(definition => definition.Name));
    }

    [Fact]
    public async Task ResolveAsync_AListWithTheHeaderOff_DoesNotShowTheHeader()
    {
        // Arrange
        var resolver = CreateResolver(CreateContactType(new JsonObject()));

        // Act
        var navigation = await resolver.ResolveAsync(CreateContact());

        // Assert
        Assert.NotNull(navigation);
        Assert.False(navigation.ShowHeader);
        Assert.Empty(navigation.ContainedContentTypeDefinitions);
    }

    [Fact]
    public async Task ResolveAsync_UsesTheDefinitionTheCallerAlreadyHas()
    {
        // Arrange
        var definitionManager = new Mock<IContentDefinitionManager>();
        var resolver = new ContactListPartNavigationResolver(definitionManager.Object);
        var contactType = CreateContactType(new JsonObject { ["ShowHeader"] = true });

        // Act
        var navigation = await resolver.ResolveAsync(CreateContact(), contactType);

        // Assert
        Assert.True(navigation.ShowHeader);
        definitionManager.Verify(manager => manager.GetTypeDefinitionAsync("Customer"), Times.Never);
    }

    private static ContactListPartNavigationResolver CreateResolver(params ContentTypeDefinition[] definitions)
    {
        var definitionManager = new Mock<IContentDefinitionManager>();

        foreach (var definition in definitions)
        {
            definitionManager
                .Setup(manager => manager.GetTypeDefinitionAsync(definition.Name))
                .ReturnsAsync(definition);
        }

        return new ContactListPartNavigationResolver(definitionManager.Object);
    }

    private static ContentItem CreateContact()
        => new() { ContentItemId = "contact-id", ContentType = "Customer" };

    private static ContentTypeDefinition CreateContactType(JsonObject listPartSettings)
    {
        var parts = new List<ContentTypePartDefinition>
        {
            CreatePart(OmnichannelConstants.ContentParts.OmnichannelContact, new JsonObject()),
        };

        if (listPartSettings is not null)
        {
            parts.Add(CreatePart("ListPart", new JsonObject { ["ListPartSettings"] = listPartSettings }));
        }

        return new ContentTypeDefinition("Customer", "Customer", parts, new JsonObject());
    }

    private static ContentTypePartDefinition CreatePart(string name, JsonObject settings)
        => new(name, new ContentPartDefinition(name, [], new JsonObject()), settings);
}
