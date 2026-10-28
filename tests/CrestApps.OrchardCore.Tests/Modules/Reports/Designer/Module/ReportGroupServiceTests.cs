using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportGroupServiceTests
{
    [Fact]
    public async Task SaveAsync_NestsAGroupInAnother()
    {
        // Arrange
        var (service, groups) = Service([new ReportGroup { ItemId = "sales", DisplayText = "Sales" }]);

        // Act
        var result = await service.SaveAsync(new ReportGroup { DisplayText = " Regional ", ParentId = "sales", Order = 3 }, null);

        // Assert
        Assert.True(result.Succeeded);
        var saved = await groups.FindByIdAsync(result.Group.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal("Regional", saved.DisplayText);
        Assert.Equal("sales", saved.ParentId);
        Assert.Equal(3, saved.Order);
        Assert.Equal("Sales / Regional", (await service.GetTreeAsync()).GetPathText(saved.ItemId));
    }

    [Theory]
    [InlineData("sales")]
    [InlineData("east")]
    public async Task SaveAsync_RefusesToPlaceAGroupInItselfOrInAGroupNestedInIt(string parentId)
    {
        // Arrange
        var sales = new ReportGroup { ItemId = "sales", DisplayText = "Sales" };
        var (service, groups) = Service([sales, new ReportGroup { ItemId = "east", DisplayText = "East", ParentId = "sales" }]);

        // Act
        var result = await service.SaveAsync(new ReportGroup { DisplayText = "Sales", ParentId = parentId }, sales.Clone());

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("A group cannot be placed in itself or in a group nested in it.", result.Errors);
        Assert.Null((await groups.FindByIdAsync("sales", TestContext.Current.CancellationToken)).ParentId);
    }

    [Fact]
    public async Task SaveAsync_RefusesAnEmptyName_AMissingParent_AndANameItsSiblingHas()
    {
        // Arrange
        var (service, _) = Service([new ReportGroup { ItemId = "sales", DisplayText = "Sales" }]);

        // Act
        var unnamed = await service.SaveAsync(new ReportGroup { DisplayText = " " }, null);
        var orphan = await service.SaveAsync(new ReportGroup { DisplayText = "Orphan", ParentId = "deleted" }, null);
        var duplicate = await service.SaveAsync(new ReportGroup { DisplayText = "SALES" }, null);
        var nested = await service.SaveAsync(new ReportGroup { DisplayText = "Sales", ParentId = "sales" }, null);

        // Assert
        Assert.Contains("Enter a name for the group.", unnamed.Errors);
        Assert.Contains("The parent group no longer exists.", orphan.Errors);
        Assert.Contains("A group named 'SALES' already exists there.", duplicate.Errors);
        Assert.True(nested.Succeeded);
    }

    [Fact]
    public async Task DeleteAsync_KeepsAGroupThatHoldsGroupsOrReports_AndDeletesAnEmptyOne()
    {
        // Arrange
        var sales = new ReportGroup { ItemId = "sales", DisplayText = "Sales" };
        var east = new ReportGroup { ItemId = "east", DisplayText = "East", ParentId = "sales" };
        var billing = new ReportGroup { ItemId = "billing", DisplayText = "Billing" };
        var empty = new ReportGroup { ItemId = "empty", DisplayText = "Empty" };
        var (service, groups) = Service([sales, east, billing, empty], [new ReportDesign { ItemId = "r1", DisplayText = "Invoices", GroupId = "billing" }]);

        // Act
        var withGroup = await service.DeleteAsync(sales);
        var withReport = await service.DeleteAsync(billing);
        var deleted = await service.DeleteAsync(empty);

        // Assert
        Assert.False(withGroup.Succeeded);
        Assert.Contains("The group 'Sales' holds 1 group(s) and 0 report(s). Move them to another group, or out of any group, before deleting it.", withGroup.Errors);
        Assert.False(withReport.Succeeded);
        Assert.True(deleted.Succeeded);
        Assert.Equal(["billing", "east", "sales"], (await groups.GetAllAsync(TestContext.Current.CancellationToken)).Select(group => group.ItemId).Order());
    }

    [Fact]
    public async Task FindOrCreatePathAsync_ReusesTheGroupsThatExist_IgnoringCase_AndCreatesTheRest()
    {
        // Arrange
        var (service, groups) = Service([new ReportGroup { ItemId = "sales", DisplayText = "Sales" }]);

        // Act
        var first = await service.FindOrCreatePathAsync(ReportGroupService.SplitPath("sales / Regional"));
        var second = await service.FindOrCreatePathAsync(["SALES", "regional"]);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(2, (await groups.GetAllAsync(TestContext.Current.CancellationToken)).Count);
        Assert.Equal("Sales / Regional", (await service.GetTreeAsync()).GetPathText(first));
        Assert.Null(await service.FindOrCreatePathAsync([]));
    }

    private static (ReportGroupService Service, ICatalog<ReportGroup> Groups) Service(ReportGroup[] groups, ReportDesign[] designs = null)
    {
        var catalog = Catalog(groups);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));

        return (new ReportGroupService(catalog, Catalog(designs ?? []), clock.Object, new PassThroughStringLocalizer<ReportGroupService>()), catalog);
    }
}
