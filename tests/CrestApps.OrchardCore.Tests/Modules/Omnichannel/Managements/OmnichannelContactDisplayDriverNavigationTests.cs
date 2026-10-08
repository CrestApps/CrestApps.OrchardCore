using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// The contact's own activity bar renders only for a contact type without the ListPart. With the ListPart, the list
/// part's bar carries the activity buttons through its contact alternate, so a second bar must not render.
/// </summary>
public sealed class OmnichannelContactDisplayDriverNavigationTests
{
    [Fact]
    public async Task GetContactNavigationContentType_AContactWithoutAList_RendersTheContactBar()
    {
        // Arrange
        var definition = CreateTypeDefinition(OmnichannelConstants.ContentParts.OmnichannelContact);
        var driver = CreateDriver(definition);

        // Act
        var result = await driver.GetContactNavigationContentTypeAsync(CreateContact());

        // Assert
        Assert.Same(definition, result);
    }

    [Fact]
    public async Task GetContactNavigationContentType_AContactThatIsAlsoAList_LeavesTheBarToTheListPart()
    {
        // Arrange
        var driver = CreateDriver(CreateTypeDefinition(OmnichannelConstants.ContentParts.OmnichannelContact, "ListPart"));

        // Act
        var result = await driver.GetContactNavigationContentTypeAsync(CreateContact());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetContactNavigationContentType_ATypeWithoutTheContactPart_RendersNoBar()
    {
        // Arrange
        var driver = CreateDriver(CreateTypeDefinition("TitlePart"));

        // Act
        var result = await driver.GetContactNavigationContentTypeAsync(CreateContact());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetContactNavigationContentType_RepeatedCalls_ReadTheDefinitionOnce()
    {
        // Arrange
        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync("Customer"))
            .ReturnsAsync(CreateTypeDefinition(OmnichannelConstants.ContentParts.OmnichannelContact, "ListPart"));

        var driver = new OmnichannelContactDisplayDriver(definitionManager.Object);

        // Act
        var first = await driver.GetContactNavigationContentTypeAsync(CreateContact());
        var second = await driver.GetContactNavigationContentTypeAsync(CreateContact());

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        definitionManager.Verify(manager => manager.GetTypeDefinitionAsync("Customer"), Times.Once);
    }

    private static OmnichannelContactDisplayDriver CreateDriver(ContentTypeDefinition definition)
    {
        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync(definition.Name))
            .ReturnsAsync(definition);

        return new OmnichannelContactDisplayDriver(definitionManager.Object);
    }

    private static ContentItem CreateContact()
    {
        var contact = new ContentItem { ContentItemId = "contact-id", ContentType = "Customer" };
        contact.Alter<OmnichannelContactPart>(_ => { });

        return contact;
    }

    private static ContentTypeDefinition CreateTypeDefinition(params string[] partNames)
    {
        return new ContentTypeDefinition(
            "Customer",
            "Customer",
            partNames.Select(name => new ContentTypePartDefinition(name, new ContentPartDefinition(name, [], new JsonObject()), new JsonObject())),
            new JsonObject());
    }
}
