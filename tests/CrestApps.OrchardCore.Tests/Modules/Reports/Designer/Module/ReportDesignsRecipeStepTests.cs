using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Recipes;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Users;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportDesignsRecipeStepTests
{
    [Fact]
    public async Task ExecuteAsync_ImportsReportsAndViews_AndMatchesOwnersByUserName()
    {
        // Arrange
        var designs = Catalog(new ReportDesign { ItemId = "4z0000000000000000000report", DisplayText = "Old title" });
        var views = Catalog<ReportView>();
        var step = Step(designs, views);
        var context = Context(new JsonObject
        {
            ["name"] = ReportDesignsRecipeStep.Name,
            ["Views"] = new JsonArray(new JsonObject
            {
                ["itemId"] = "4z00000000000000000000view",
                ["displayText"] = "Revenue view",
                ["ownerId"] = "id-on-the-other-site",
                ["OwnerUserName"] = "alice",
            }),
            ["Reports"] = new JsonArray(
                new JsonObject
                {
                    ["itemId"] = "4z0000000000000000000report",
                    ["displayText"] = "New title",
                    ["showInAdminMenu"] = true,
                    ["sharedRoles"] = new JsonArray("Sales"),
                    ["visuals"] = new JsonArray(new JsonObject { ["id"] = "v1", ["type"] = "Chart", ["chartType"] = "Pie", ["width"] = 40 }),
                    ["OwnerUserName"] = "nobody",
                    ["ownerId"] = "kept",
                },
                new JsonObject { ["displayText"] = "No id" }),
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var view = Assert.Single(await views.GetAllAsync(TestContext.Current.CancellationToken));
        Assert.Equal("alice-id", view.OwnerId);

        var design = Assert.Single(await designs.GetAllAsync(TestContext.Current.CancellationToken));
        Assert.Equal("New title", design.DisplayText);
        Assert.True(design.ShowInAdminMenu);
        Assert.Equal(["Sales"], design.SharedRoles);
        Assert.Equal("kept", design.OwnerId);
        Assert.Equal(ReportVisualType.Chart, design.Visuals[0].Type);
        Assert.Equal(12, design.Visuals[0].Width);

        Assert.Single(context.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_ImportsTheRefreshScheduleOfViews_NeverShorterThanTheShortestOne()
    {
        // Arrange
        var views = Catalog<ReportView>();
        var step = Step(Catalog<ReportDesign>(), views);
        var context = Context(new JsonObject
        {
            ["name"] = ReportDesignsRecipeStep.Name,
            ["Views"] = new JsonArray(
                new JsonObject { ["itemId"] = "hourly", ["displayText"] = "Hourly", ["refreshIntervalMinutes"] = 60 },
                new JsonObject { ["itemId"] = "too-often", ["displayText"] = "Too often", ["refreshIntervalMinutes"] = 1 },
                new JsonObject { ["itemId"] = "live", ["displayText"] = "Live" }),
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Equal(60, (await views.FindByIdAsync("hourly", TestContext.Current.CancellationToken)).RefreshIntervalMinutes);
        Assert.Equal(ReportViewRefreshIntervals.Minimum, (await views.FindByIdAsync("too-often", TestContext.Current.CancellationToken)).RefreshIntervalMinutes);
        Assert.Equal(ReportViewRefreshIntervals.Live, (await views.FindByIdAsync("live", TestContext.Current.CancellationToken)).RefreshIntervalMinutes);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_GivesTheItemsThatNameNoOwnerToThePersonWhoRunsTheRecipe()
    {
        // Arrange
        var designs = Catalog<ReportDesign>();
        var importer = User("importer-id", "importer");
        var step = Step(designs, Catalog<ReportView>(), importer);
        var context = Context(new JsonObject
        {
            ["name"] = ReportDesignsRecipeStep.Name,
            ["Reports"] = new JsonArray(
                new JsonObject { ["itemId"] = "ready-made", ["displayText"] = "Ready-made" },
                new JsonObject { ["itemId"] = "unknown-owner", ["displayText"] = "Unknown owner", ["OwnerUserName"] = "nobody" },
                new JsonObject { ["itemId"] = "owned", ["displayText"] = "Owned", ["OwnerUserName"] = "alice" },
                new JsonObject { ["itemId"] = "kept", ["displayText"] = "Kept", ["ownerId"] = "kept-id" }),
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Equal("importer-id", (await designs.FindByIdAsync("ready-made", TestContext.Current.CancellationToken)).OwnerId);
        Assert.Equal("importer-id", (await designs.FindByIdAsync("unknown-owner", TestContext.Current.CancellationToken)).OwnerId);
        Assert.Equal("alice-id", (await designs.FindByIdAsync("owned", TestContext.Current.CancellationToken)).OwnerId);
        Assert.Equal("kept-id", (await designs.FindByIdAsync("kept", TestContext.Current.CancellationToken)).OwnerId);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutASignedInPerson_LeavesTheItemsThatNameNoOwnerWithoutOne()
    {
        // Arrange
        var designs = Catalog<ReportDesign>();
        var step = Step(designs, Catalog<ReportView>());
        var context = Context(new JsonObject
        {
            ["name"] = ReportDesignsRecipeStep.Name,
            ["Reports"] = new JsonArray(new JsonObject { ["itemId"] = "ready-made", ["displayText"] = "Ready-made" }),
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Null((await designs.FindByIdAsync("ready-made", TestContext.Current.CancellationToken)).OwnerId);
    }

    [Fact]
    public async Task ExecuteAsync_ImportsGroups_AndPlacesEachReportInTheGroupItNames()
    {
        // Arrange
        var designs = Catalog<ReportDesign>();
        var groups = Catalog(new ReportGroup { ItemId = "store", DisplayText = "Store" });
        var step = Step(designs, Catalog<ReportView>(), groups: groups);
        var context = Context(new JsonObject
        {
            ["name"] = ReportDesignsRecipeStep.Name,
            ["Groups"] = new JsonArray(
                // The nested group comes first: parents are checked once every group is in.
                new JsonObject { ["itemId"] = "regional", ["displayText"] = "Regional", ["parentId"] = "sales", ["order"] = 2 },
                new JsonObject { ["itemId"] = "sales", ["displayText"] = "Sales" },
                new JsonObject { ["itemId"] = "loop-a", ["displayText"] = "Loop A", ["parentId"] = "loop-b" },
                new JsonObject { ["itemId"] = "loop-b", ["displayText"] = "Loop B", ["parentId"] = "loop-a" }),
            ["Reports"] = new JsonArray(
                new JsonObject { ["itemId"] = "by-id", ["displayText"] = "By id", ["groupId"] = "regional" },
                new JsonObject { ["itemId"] = "by-path", ["displayText"] = "By path", ["group"] = "store / Inventory" },
                new JsonObject { ["itemId"] = "by-category", ["displayText"] = "By category", ["category"] = "Billing" },
                new JsonObject { ["itemId"] = "missing", ["displayText"] = "Missing", ["groupId"] = "deleted" }),
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        var tree = new ReportGroupTree(await groups.GetAllAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Sales / Regional", tree.GetPathText("regional"));
        Assert.Equal(2, tree.Find("regional").Order);
        Assert.Null(tree.Find("loop-a").ParentId);
        Assert.Null(tree.Find("loop-b").ParentId);

        async Task<string> PathOf(string id) => tree.GetPathText((await designs.FindByIdAsync(id, TestContext.Current.CancellationToken)).GroupId);

        Assert.Equal("Sales / Regional", await PathOf("by-id"));
        Assert.Equal("Store / Inventory", await PathOf("by-path"));
        Assert.Equal("Billing", await PathOf("by-category"));
        Assert.Null(await PathOf("missing"));
#pragma warning disable CS0618 // The category of a report written before report groups is cleared once it names its group.
        Assert.Null((await designs.FindByIdAsync("by-category", TestContext.Current.CancellationToken)).Category);
#pragma warning restore CS0618
    }

    private static ReportDesignsRecipeStep Step(
        CrestApps.Core.Services.ICatalog<ReportDesign> designs,
        CrestApps.Core.Services.ICatalog<ReportView> views,
        System.Security.Claims.ClaimsPrincipal importer = null,
        CrestApps.Core.Services.ICatalog<ReportGroup> groups = null)
    {
        var alice = Mock.Of<IUser>();
        var importerUser = Mock.Of<IUser>();
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

        userManager.Setup(manager => manager.FindByNameAsync("alice")).ReturnsAsync(alice);
        userManager.Setup(manager => manager.GetUserIdAsync(alice)).ReturnsAsync("alice-id");
        userManager.Setup(manager => manager.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(importerUser);
        userManager.Setup(manager => manager.GetUserIdAsync(importerUser)).ReturnsAsync("importer-id");

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = importer is null ? null : new DefaultHttpContext { User = importer },
        };

        var groupService = new ReportGroupService(groups ?? Catalog<ReportGroup>(), designs, clock.Object, new PassThroughStringLocalizer<ReportGroupService>());

        return new ReportDesignsRecipeStep(designs, views, groupService, NoSnapshots(), userManager.Object, httpContextAccessor, clock.Object, new PassThroughStringLocalizer<ReportDesignsRecipeStep>());
    }

    private static RecipeExecutionContext Context(JsonObject step)
    {
        return new RecipeExecutionContext
        {
            Name = ReportDesignsRecipeStep.Name,
            Step = step,
        };
    }
}
