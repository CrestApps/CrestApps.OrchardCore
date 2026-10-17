using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Recipes;
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

    private static ReportDesignsRecipeStep Step(CrestApps.Core.Services.ICatalog<ReportDesign> designs, CrestApps.Core.Services.ICatalog<ReportView> views)
    {
        var alice = Mock.Of<IUser>();
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

        userManager.Setup(manager => manager.FindByNameAsync("alice")).ReturnsAsync(alice);
        userManager.Setup(manager => manager.GetUserIdAsync(alice)).ReturnsAsync("alice-id");

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));

        return new ReportDesignsRecipeStep(designs, views, NoSnapshots(), userManager.Object, clock.Object, new PassThroughStringLocalizer<ReportDesignsRecipeStep>());
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
