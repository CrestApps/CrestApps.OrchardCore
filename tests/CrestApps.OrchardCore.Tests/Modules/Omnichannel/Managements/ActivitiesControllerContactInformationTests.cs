using CrestApps.OrchardCore.Omnichannel.Managements.Controllers;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// Bug: on the activity pages, the contact information card of a contact type with the ListPart also rendered the
/// items listed under the contact, because the card renders the contact's Detail display. The card now leaves the
/// list part's shape out and keeps everything else. Confirmed live; this pins which shapes are removed.
/// </summary>
public sealed class ActivitiesControllerContactInformationTests
{
    [Fact]
    public async Task RemoveListPartShapes_LeavesTheListOutAndKeepsTheContactsFields()
    {
        // Arrange
        var contactShape = new ZoneHolding(() => ValueTask.FromResult<IShape>(new Shape()));
        await contactShape.Zones["Content"].AddAsync(CreateShape("TextField", "Customer-Story"), "1");
        await contactShape.Zones["Content"].AddAsync(CreateShape("ListPart", "ListPart"), "10");
        await contactShape.Zones["Content"].AddAsync(CreateShape("BagPart", "ContactMethods"), "5");

        // Act
        ActivitiesController.RemoveListPartShapes(contactShape);

        // Assert
        var content = Assert.IsType<Shape>(contactShape.Zones["Content"], exactMatch: false);
        Assert.Equal(
            ["TextField", "BagPart"],
            content.Items.OfType<IShape>().Select(shape => shape.Metadata.Type));
    }

    [Fact]
    public void RemoveListPartShapes_AContactWithNoContent_IsLeftAlone()
    {
        // Arrange
        var contactShape = new ZoneHolding(() => ValueTask.FromResult<IShape>(new Shape()));

        // Act
        var exception = Record.Exception(() => ActivitiesController.RemoveListPartShapes(contactShape));

        // Assert
        Assert.Null(exception);
    }

    private static Shape CreateShape(string type, string name)
    {
        var shape = new Shape();
        shape.Metadata.Type = type;
        shape.Metadata.Name = name;

        return shape;
    }
}
