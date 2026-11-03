using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportGroupTreeTests
{
    private static readonly ReportGroup[] _groups =
    [
        new() { ItemId = "sales", DisplayText = "Sales", Order = 2 },
        new() { ItemId = "regional", DisplayText = "Regional", ParentId = "sales" },
        new() { ItemId = "east", DisplayText = "East", ParentId = "regional" },
        new() { ItemId = "billing", DisplayText = "Billing", Order = 2 },
        new() { ItemId = "executive", DisplayText = "Executive", Order = 1 },
    ];

    [Fact]
    public void Flatten_ListsEachGroupBeforeTheGroupsNestedInIt_ByOrderThenName()
    {
        // Arrange
        var tree = new ReportGroupTree(_groups);

        // Act
        var nodes = tree.Flatten();

        // Assert
        Assert.Equal(["Executive", "Billing", "Sales", "Sales / Regional", "Sales / Regional / East"], nodes.Select(node => node.PathText));
        Assert.Equal([0, 0, 0, 1, 2], nodes.Select(node => node.Depth));
    }

    [Fact]
    public void GetPathText_NamesEveryGroupFromTheTopLevel()
    {
        // Arrange
        var tree = new ReportGroupTree(_groups);

        // Act & Assert
        Assert.Equal("Sales / Regional / East", tree.GetPathText("east"));
        Assert.Equal("Billing", tree.GetPathText("billing"));
        Assert.Null(tree.GetPathText("deleted"));
        Assert.Null(tree.GetPathText(null));
    }

    [Fact]
    public void IsSelfOrDescendant_FindsTheGroupsAGroupCannotBeMovedInto()
    {
        // Arrange
        var tree = new ReportGroupTree(_groups);

        // Act & Assert
        Assert.True(tree.IsSelfOrDescendant("sales", "sales"));
        Assert.True(tree.IsSelfOrDescendant("east", "sales"));
        Assert.False(tree.IsSelfOrDescendant("sales", "east"));
        Assert.False(tree.IsSelfOrDescendant("billing", "sales"));
    }

    [Fact]
    public void GroupsWhoseParentsLeadBackToThemselves_AreTopLevel_SoTheTreeNeverLoops()
    {
        // Arrange: only an imported recipe can store such groups.
        var tree = new ReportGroupTree(
        [
            new ReportGroup { ItemId = "a", DisplayText = "A", ParentId = "b" },
            new ReportGroup { ItemId = "b", DisplayText = "B", ParentId = "a" },
            new ReportGroup { ItemId = "c", DisplayText = "C", ParentId = "a" },
            new ReportGroup { ItemId = "d", DisplayText = "D", ParentId = "missing" },
        ]);

        // Act
        var nodes = tree.Flatten();

        // Assert
        Assert.Equal(["A", "A / C", "B", "D"], nodes.Select(node => node.PathText));
        Assert.Null(tree.GetParentId("a"));
        Assert.Equal("a", tree.GetParentId("c"));
    }
}
