using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Deployments;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using OrchardCore.Users;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportDesignsDeploymentSourceTests
{
    [Fact]
    public async Task ByDefault_ExportsEveryReportAndView()
    {
        // Arrange
        var step = new ReportDesignsDeploymentStep();

        // Act
        var exported = await ExportAsync(step);

        // Assert
        Assert.Equal(["Activities by agent", "Campaigns", "Plain report"], Titles(exported, "Reports"));
        Assert.Equal(["Activities with agents", "Agents", "Unused view"], Titles(exported, "Views"));
        Assert.Equal(["Other", "Sales", "Regional"], Titles(exported, "Groups"));
    }

    [Fact]
    public async Task PickedReports_AreExportedWithTheirGroups_AndTheGroupsThoseAreNestedIn()
    {
        // Arrange
        var step = new ReportDesignsDeploymentStep
        {
            IncludeAll = false,
            ReportIds = ["campaigns"],
        };

        // Act
        var exported = await ExportAsync(step);

        // Assert: parents first, so the import finds each parent before the groups nested in it.
        Assert.Equal(["Sales", "Regional"], Titles(exported, "Groups"));
        Assert.Equal("regional", exported["Reports"][0]["groupId"]?.GetValue<string>());
        Assert.Equal("sales", exported["Groups"][1]["parentId"]?.GetValue<string>());
    }

    [Fact]
    public async Task PickedReports_AreExportedWithTheViewsTheyRead_AndTheViewsThoseViewsRead()
    {
        // Arrange
        var step = new ReportDesignsDeploymentStep
        {
            IncludeAll = false,
            ReportIds = ["by-agent"],
        };

        // Act
        var exported = await ExportAsync(step);

        // Assert
        Assert.Equal(["Activities by agent"], Titles(exported, "Reports"));
        Assert.Equal(["Activities with agents", "Agents"], Titles(exported, "Views"));
        Assert.Empty(Titles(exported, "Groups"));
    }

    [Fact]
    public async Task PickedViews_AreExportedWithoutReports()
    {
        // Arrange
        var step = new ReportDesignsDeploymentStep
        {
            IncludeAll = false,
            ViewIds = ["unused-view"],
        };

        // Act
        var exported = await ExportAsync(step);

        // Assert
        Assert.Empty(Titles(exported, "Reports"));
        Assert.Equal(["Unused view"], Titles(exported, "Views"));
    }

    [Fact]
    public void APlanSavedBeforeReportsCouldBePicked_StillExportsEverything()
    {
        // Act: a step saved without the selection properties.
        var step = JsonSerializer.Deserialize<ReportDesignsDeploymentStep>("""{"Name":"ReportDesigns","Id":"step-1"}""");

        // Assert
        Assert.True(step.IncludeAll);
    }

    private static async Task<JsonObject> ExportAsync(ReportDesignsDeploymentStep step)
    {
        var designs = Catalog(
            Report("by-agent", "Activities by agent", ("V", ReportsConstants.ViewsDataSource, "with-agents")),
            new ReportDesign
            {
                ItemId = "campaigns",
                DisplayText = "Campaigns",
                GroupId = "regional",
                Query = Query([("C", "Omnichannel", "Campaigns")]),
            },
            Report("plain", "Plain report"));

        var views = Catalog(
            View("with-agents", "Activities with agents", ("A", ReportsConstants.ViewsDataSource, "agents")),
            View("agents", "Agents"),
            View("unused-view", "Unused view"));

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        var groups = Catalog(
            new ReportGroup { ItemId = "sales", DisplayText = "Sales" },
            new ReportGroup { ItemId = "regional", DisplayText = "Regional", ParentId = "sales" },
            new ReportGroup { ItemId = "other", DisplayText = "Other" });

        var source = new ReportDesignsDeploymentSource(designs, views, groups, userManager.Object);
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await source.ProcessDeploymentStepAsync(step, result);

        return Assert.Single(result.Steps);
    }

    private static string[] Titles(JsonObject step, string property)
    {
        return [.. step[property].AsArray().Select(node => node["displayText"]?.GetValue<string>())];
    }

    private static ReportDesign Report(string id, string title, params (string Alias, string Source, string DataSet)[] dataSets)
    {
        return new ReportDesign
        {
            ItemId = id,
            DisplayText = title,
            Query = Query(dataSets),
        };
    }

    private static ReportView View(string id, string title, params (string Alias, string Source, string DataSet)[] dataSets)
    {
        return new ReportView
        {
            ItemId = id,
            DisplayText = title,
            Query = Query(dataSets),
        };
    }

    private static ReportQueryDefinition Query((string Alias, string Source, string DataSet)[] dataSets)
    {
        return new ReportQueryDefinition
        {
            DataSets = [.. dataSets.Select(dataSet => new ReportDataSetReference
            {
                Alias = dataSet.Alias,
                Source = dataSet.Source,
                DataSet = dataSet.DataSet,
            })],
        };
    }
}
