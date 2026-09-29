using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Lists.ViewModels;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// Bug: a contact type that also had the ListPart showed two navigation bars on its editor, the list part's bar and
/// the contact's activity bar, because Orchard Core's list bar cannot be extended. The list bar now takes a contact
/// alternate that carries both sets of buttons, and the list header takes one that shows the contact's primary
/// contact methods. Confirmed live; these pin when each alternate is added.
/// </summary>
public sealed class ContactListPartShapeTableProviderTests
{
    [Fact]
    public async Task ListNavigation_WhenTheContainerIsAContact_AddsTheContactAlternate()
    {
        // Arrange
        var shape = CreateNavigationShape(CreateTypeDefinition(OmnichannelConstants.ContentParts.OmnichannelContact, "ListPart"));

        // Act
        await DisplayAsync(ContactListPartShapeTableProvider.NavigationShapeType, shape);

        // Assert
        Assert.Contains(ContactListPartShapeTableProvider.NavigationContactAlternate, shape.Metadata.Alternates);
    }

    [Fact]
    public async Task ListNavigation_WhenTheContainerIsNotAContact_KeepsOrchardCoresBar()
    {
        // Arrange
        var shape = CreateNavigationShape(CreateTypeDefinition("ListPart"));

        // Act
        await DisplayAsync(ContactListPartShapeTableProvider.NavigationShapeType, shape);

        // Assert
        Assert.DoesNotContain(ContactListPartShapeTableProvider.NavigationContactAlternate, shape.Metadata.Alternates);
    }

    [Fact]
    public async Task ListHeader_WhenTheItemIsAContact_AddsTheContactAlternateBelowTheTypeAlternates()
    {
        // Arrange
        var contact = new ContentItem { ContentType = "Customer" };
        contact.Alter<OmnichannelContactPart>(_ => { });

        var shape = new Shape();
        shape.Properties["ContentItem"] = contact;
        shape.Metadata.Alternates.Add("Content_HeaderAdmin__Customer");

        // Act
        await DisplayAsync(ContactListPartShapeTableProvider.HeaderShapeType, shape);

        // Assert
        // The last alternate wins, so a site's own Content-Customer.HeaderAdmin template still takes precedence.
        Assert.Equal(
            [ContactListPartShapeTableProvider.HeaderContactAlternate, "Content_HeaderAdmin__Customer"],
            shape.Metadata.Alternates);
    }

    [Fact]
    public async Task ListHeader_WhenTheItemIsNotAContact_LeavesTheAlternatesAlone()
    {
        // Arrange
        var shape = new Shape();
        shape.Properties["ContentItem"] = new ContentItem { ContentType = "Article" };
        shape.Metadata.Alternates.Add("Content_HeaderAdmin__Article");

        // Act
        await DisplayAsync(ContactListPartShapeTableProvider.HeaderShapeType, shape);

        // Assert
        Assert.Equal(["Content_HeaderAdmin__Article"], shape.Metadata.Alternates);
    }

    private static async Task DisplayAsync(string shapeType, IShape shape)
    {
        var builder = new ShapeTableBuilder(Mock.Of<IFeatureInfo>());
        await new ContactListPartShapeTableProvider().DiscoverAsync(builder);

        var descriptor = new ShapeDescriptor { ShapeType = shapeType };

        foreach (var alteration in builder.BuildAlterations().Where(alteration => alteration.ShapeType == shapeType))
        {
            alteration.Alter(descriptor);
        }

        Assert.NotEmpty(descriptor.DisplayingAsync);

        foreach (var displaying in descriptor.DisplayingAsync)
        {
            await displaying(new ShapeDisplayContext { Shape = shape });
        }
    }

    // The shape factory's shape for a typed view model is a proxy that is both the view model and an IShape.
    private static IShape CreateNavigationShape(ContentTypeDefinition containerDefinition)
    {
        var shape = new Mock<ListPartNavigationAdminViewModel>();
        shape.As<IShape>().SetupGet(x => x.Metadata).Returns(new ShapeMetadata());

        shape.Object.Container = new ContentItem { ContentType = containerDefinition.Name };
        shape.Object.ContainerContentTypeDefinition = containerDefinition;

        return (IShape)shape.Object;
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
