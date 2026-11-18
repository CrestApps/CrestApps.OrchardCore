using System.Text.Json;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.AI;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using AgentToolTestContext = CrestApps.OrchardCore.Tests.Agent.AgentToolTestContext;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.AI;

/// <summary>
/// The Reports and Report Builder tools check the permission the report pages check, with the report or view as the
/// resource; run a built-in report for a period and a designed report with its owner's access; cap what they return;
/// never change sharing outside shareReportDesign; never return a share link's address; and do not touch a report
/// changed earlier in the same chat message.
/// </summary>
public sealed class ReportsAIToolTests : IAsyncLifetime
{
    private static readonly string _design = ReportDesignerPermissions.ManageOwnReportDesigns.Name;
    private static readonly string _manageAll = ReportDesignerPermissions.ManageAllReportDesigns.Name;
    private static readonly string _viewAll = ReportDesignerPermissions.ViewAllReportDesigns.Name;
    private static readonly string _groups = ReportDesignerPermissions.ManageReportGroups.Name;
    private static readonly string _sharePublicly = ReportDesignerPermissions.ShareReportsPublicly.Name;
    private static readonly string _viewSales = SalesTestReport.Permission.Name;
    private static readonly string _exportSales = SalesTestReport.ExportPermission.Name;

    private ReportToolHarness _harness;

    public static TheoryData<string> ToolNames => new()
    {
        ListReportsTool.TheName,
        GetReportTool.TheName,
        RunReportTool.TheName,
        ExportReportTool.TheName,
        ListReportDataSetsTool.TheName,
        GetReportDataSetSchemaTool.TheName,
        QueryReportDataTool.TheName,
        SaveReportDesignTool.TheName,
        ShareReportDesignTool.TheName,
        RestoreReportDesignVersionTool.TheName,
        DeleteReportDesignTool.TheName,
        CreateReportShareLinkTool.TheName,
        RevokeReportShareLinkTool.TheName,
        ListReportViewsTool.TheName,
        SaveReportViewTool.TheName,
        RefreshReportViewTool.TheName,
        DeleteReportViewTool.TheName,
        ListReportGroupsTool.TheName,
        SaveReportGroupTool.TheName,
        DeleteReportGroupTool.TheName,
    };

    public async ValueTask InitializeAsync()
    {
        _harness = await ReportToolHarness.CreateAsync();

        await _harness.Designs.CreateAsync(ReportToolHarness.Report(), TestContext.Current.CancellationToken);
        await _harness.Views.CreateAsync(new ReportView
        {
            ItemId = "v1",
            DisplayText = "Orders view",
            OwnerId = ReportToolHarness.OwnerId,
            Query = ReportToolHarness.Report().Query,
        }, TestContext.Current.CancellationToken);
        await _harness.Groups.CreateAsync(new ReportGroup { ItemId = "g1", DisplayText = "Sales" }, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var services = _harness.Build(signedIn: false, _design, _manageAll, _viewAll, _groups, _sharePublicly, _viewSales, _exportSales);

        // Act
        var result = await InvokeAsync(toolName, services, AllArguments("r1"));

        // Assert
        Assert.Equal(ReportsAIToolBase.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        // Arrange: the caller holds no permission and neither owns nor shares the report, the view or the built-in report.
        var services = _harness.Build();
        var id = toolName switch
        {
            ListReportsTool.TheName or GetReportTool.TheName or RunReportTool.TheName or ExportReportTool.TheName => SalesTestReport.Name,
            ListReportViewsTool.TheName or SaveReportViewTool.TheName or RefreshReportViewTool.TheName or DeleteReportViewTool.TheName => "v1",
            ListReportGroupsTool.TheName or SaveReportGroupTool.TheName or DeleteReportGroupTool.TheName => "g1",
            _ => "r1",
        };

        // Act
        var result = await InvokeAsync(toolName, services, AllArguments(id));

        // Assert
        if (toolName == ListReportsTool.TheName)
        {
            // The list is not refused: it lists only what the caller may open, which is nothing.
            Assert.Equal(0, AgentToolTestContext.ReadJson(result).GetProperty("total").GetInt32());
        }
        else
        {
            Assert.Equal(ReportsAIToolBase.NotAllowedMessage, result);
        }

        Assert.Equal("Sales by customer", (await _harness.Designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);
        Assert.NotNull(await _harness.Views.FindByIdAsync("v1", TestContext.Current.CancellationToken));
        Assert.NotNull(await _harness.Groups.FindByIdAsync("g1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListReports_ListsTheBuiltInReportsAndTheDesignedReportsSharedWithTheCaller()
    {
        // Arrange
        var shared = ReportToolHarness.Report("r2", "Shared with Ada");
        shared.SharedUserNames = [ReportToolHarness.CallerName];
        await _harness.Designs.CreateAsync(shared, TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _viewSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListReportsTool.TheName, services, new()));

        // Assert
        var ids = json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToArray();
        Assert.Equal([SalesTestReport.Name, "r2"], ids);
    }

    [Fact]
    public async Task GetReport_DescribesABuiltInReportsPeriodAndExportFormats()
    {
        // Arrange
        var services = _harness.Build(true, _viewSales, _exportSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetReportTool.TheName, services, new() { ["id"] = SalesTestReport.Name }));

        // Assert
        Assert.Equal("builtIn", json.GetProperty("kind").GetString());
        Assert.Equal("last7", json.GetProperty("defaultPeriod").GetString());
        Assert.True(json.GetProperty("acceptsAllTime").GetBoolean());
        Assert.Equal("csv", json.GetProperty("exportFormats")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetReport_LeavesOutTheShareLinks_WithoutThePermissionToShareReportsPublicly()
    {
        // Arrange: the caller owns the report and designs reports.
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetReportTool.TheName, services, new() { ["id"] = "r1", ["include"] = new[] { "definition", "shareLinks" } }));

        // Assert
        Assert.True(json.TryGetProperty("definition", out _));
        Assert.False(json.TryGetProperty("shareLinks", out _));
        Assert.Contains("share reports publicly", json.GetProperty("notIncluded")[0].GetString());
    }

    [Fact]
    public async Task RunReport_RunsABuiltInReportForTheGivenDays_AndCapsTheRows()
    {
        // Arrange
        var services = _harness.Build(true, _viewSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(RunReportTool.TheName, services, new() { ["id"] = SalesTestReport.Name, ["from"] = "2026-03-01", ["to"] = "2026-03-10", ["maxRows"] = 3 }));

        // Assert
        Assert.StartsWith("2026-03-01T00:00:00", json.GetProperty("period").GetProperty("from").GetString());
        Assert.StartsWith("2026-03-10T23:59:59", json.GetProperty("period").GetProperty("to").GetString());
        var table = json.GetProperty("sections")[1];
        Assert.Equal(10, table.GetProperty("rowCount").GetInt32());
        Assert.Equal(3, table.GetProperty("rows").GetArrayLength());
        Assert.True(json.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task RunReport_RefusesFiltersOnABuiltInReport()
    {
        // Arrange
        var services = _harness.Build(true, _viewSales);

        // Act
        var result = await InvokeAsync(RunReportTool.TheName, services, new() { ["id"] = SalesTestReport.Name, ["filters"] = new Dictionary<string, string[]> { ["profile"] = ["x"] } });

        // Assert
        Assert.Contains("only the period", result);
    }

    [Fact]
    public async Task RunReport_RunsADesignedReportSharedWithTheCaller_WithItsOwnersAccess()
    {
        // Arrange
        var report = ReportToolHarness.Report();
        report.SharedRoles = ["Authenticated"];
        await _harness.Designs.UpdateAsync(report, TestContext.Current.CancellationToken);
        var services = _harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(RunReportTool.TheName, services, new() { ["id"] = "r1" }));

        // Assert
        Assert.Equal("designed", json.GetProperty("kind").GetString());
        Assert.Equal(3, json.GetProperty("rowCount").GetInt32());
        Assert.Equal(2, json.GetProperty("columns").GetArrayLength());
    }

    [Fact]
    public async Task RunReport_RefusesAFilterTheDesignedReportDoesNotExpose()
    {
        // Arrange
        var services = _harness.Build(true, _viewAll);

        // Act
        var result = await InvokeAsync(RunReportTool.TheName, services, new() { ["id"] = "r1", ["filters"] = new Dictionary<string, string[]> { ["region"] = ["West"] } });

        // Assert
        Assert.Contains("no filters its viewers can change", result);
    }

    [Fact]
    public async Task ExportReport_ReturnsTheReportPagesExportAddress_WithThePeriod()
    {
        // Arrange
        var services = _harness.Build(true, _viewSales, _exportSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ExportReportTool.TheName, services, new() { ["id"] = SalesTestReport.Name, ["from"] = "2026-03-01", ["to"] = "2026-03-10" }));

        // Assert
        var url = json.GetProperty("downloadUrl").GetString();
        Assert.StartsWith("/Admin/Reports/view/salesTest/export/csv?", url);
        Assert.Contains("ReportFilter.From=2026-03-01T00%3A00%3A00", url);
        Assert.Contains("ReportFilter.To=2026-03-10T23%3A59%3A59", url);
        Assert.Contains("ReportFilter.Range=custom", url);
        Assert.Equal("salesTest-20260301-to-20260310.csv", json.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task ExportReport_RefusesACallerWithoutTheExportPermission()
    {
        // Arrange
        var services = _harness.Build(true, _viewSales);

        // Act
        var result = await InvokeAsync(ExportReportTool.TheName, services, new() { ["id"] = SalesTestReport.Name });

        // Assert
        Assert.Equal(ReportsAIToolBase.NotAllowedMessage, result);
    }

    [Fact]
    public async Task ExportReport_SendsEveryExposedFilterOfADesignedReport_AsTheFilterFormDoes()
    {
        // Arrange
        var report = ReportToolHarness.Report();
        report.Query.Filters =
        [
            new ReportFilterDefinition { Id = "region", Field = "c.Region", Operator = DataFilterOperator.In, Exposed = true, Control = ReportFilterControl.MultiSelect, Values = ["West"] },
            new ReportFilterDefinition { Id = "placed", Field = "o.PlacedUtc", Operator = DataFilterOperator.Between, Exposed = true, Control = ReportFilterControl.DateRange },
        ];
        await _harness.Designs.UpdateAsync(report, TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _viewAll);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ExportReportTool.TheName, services, new() { ["id"] = "r1", ["format"] = "csv", ["from"] = "2026-01-01", ["to"] = "2026-01-31" }));

        // Assert
        var url = json.GetProperty("downloadUrl").GetString();
        Assert.StartsWith("/Admin/reports/designs/r1/export/csv?applied=1", url);
        Assert.Contains("f.region=West", url);
        Assert.Contains("f.placed.from=2026-01-01", url);
        Assert.Contains("f.placed.to=2026-01-31", url);
        Assert.Equal("placed", json.GetProperty("periodFilter").GetString());
    }

    [Fact]
    public async Task ListReportDataSets_ListsTheDataSetsOfEachSource()
    {
        // Arrange
        var services = _harness.Build(true, _design);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListReportDataSetsTool.TheName, services, new() { ["query"] = "ord" }));

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("Memory", item.GetProperty("source").GetString());
        Assert.Equal("Order", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetReportDataSetSchema_ReturnsTheFields_AndSaysWhenTheDataSetIsUnknown()
    {
        // Arrange
        var services = _harness.Build(true, _design);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetReportDataSetSchemaTool.TheName, services, new() { ["source"] = "Memory", ["dataSet"] = "Order", ["includeFunctions"] = false }));
        var missing = await InvokeAsync(GetReportDataSetSchemaTool.TheName, services, new() { ["source"] = "Nope", ["dataSet"] = "Order" });

        // Assert
        Assert.Contains(json.GetProperty("fields").EnumerateArray(), field => field.GetProperty("name").GetString() == "Total" && field.GetProperty("dataType").GetString() == "Decimal");
        Assert.Contains("There is no data source 'Nope'", missing);
    }

    [Fact]
    public async Task QueryReportData_GroupsAndSums_WithTheCallersAccess()
    {
        // Arrange
        var services = _harness.Build(true, _design);
        var query = JsonSerializer.SerializeToElement(new
        {
            dataSets = new[] { new { alias = "o", source = "Memory", dataSet = "Order" } },
            columns = new object[]
            {
                new { id = "customer", field = "o.CustomerId" },
                new { id = "total", field = "o.Total", aggregate = "Sum" },
            },
            sorts = new[] { new { columnId = "total", descending = true } },
        });

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(QueryReportDataTool.TheName, services, new() { ["query"] = query }));
        var invalid = await InvokeAsync(QueryReportDataTool.TheName, services, new() { ["query"] = JsonSerializer.SerializeToElement(new { columns = Array.Empty<object>() }) });

        // Assert
        Assert.Equal(4, json.GetProperty("rowCount").GetInt32());
        Assert.Equal("c2", json.GetProperty("rows")[0][0].GetString());
        Assert.Equal(300m, json.GetProperty("rows")[0][1].GetDecimal());
        Assert.Contains("reads no data set", invalid);
    }

    [Fact]
    public async Task SaveReportDesign_CreatesAndPublishesAReport_SharedWithNobody()
    {
        // Arrange
        var services = _harness.Build(true, _design);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportDesignTool.TheName, services, new()
        {
            ["title"] = "Monthly sales",
            ["group"] = "Sales",
            ["query"] = JsonSerializer.SerializeToElement(ReportToolHarness.Report().Query, CrestApps.OrchardCore.Reports.Designer.ViewModels.ReportDesignerJson.Options),
        }));

        // Assert
        Assert.True(json.GetProperty("saved").GetBoolean());
        var saved = await _harness.Designs.FindByIdAsync(json.GetProperty("id").GetString(), TestContext.Current.CancellationToken);
        Assert.Equal(ReportToolHarness.CallerId, saved.OwnerId);
        Assert.Equal("g1", saved.GroupId);
        Assert.Empty(saved.SharedUserNames);
        Assert.Empty(saved.SharedRoles);
        Assert.Equal(1, json.GetProperty("versionNumber").GetInt32());
    }

    [Fact]
    public async Task SaveReportDesign_ChangingAReport_KeepsWhoItIsSharedWith()
    {
        // Arrange
        var report = ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId);
        report.SharedUserNames = ["Jane"];
        report.SharedRoles = ["Sales Manager"];
        await _harness.Designs.UpdateAsync(report, TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportDesignTool.TheName, services, new() { ["id"] = "r1", ["title"] = "Renamed" }));

        // Assert
        Assert.True(json.GetProperty("saved").GetBoolean());
        var saved = await _harness.Designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);
        Assert.Equal("Renamed", saved.DisplayText);
        Assert.Equal(["Jane"], saved.SharedUserNames);
        Assert.Equal(["Sales Manager"], saved.SharedRoles);
    }

    [Fact]
    public async Task SaveReportDesign_RefusesAReportWithUnpublishedChangesInTheBuilder()
    {
        // Arrange
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design);
        var history = services.GetRequiredService<ReportDesignHistoryService>();
        var design = await _harness.Designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);
        var draft = design.Clone();
        draft.DisplayText = "Work in progress";
        await history.SaveDraftAsync(design, draft, 0, force: false, ReportDesignerPrincipals.User("bob", "Bob"));

        // Act
        var result = await InvokeAsync(SaveReportDesignTool.TheName, services, new() { ["id"] = "r1", ["title"] = "Renamed" });

        // Assert
        Assert.Contains("unpublished changes in the Report Builder by Bob", result);
        Assert.Equal("Sales by customer", (await _harness.Designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);
    }

    [Fact]
    public async Task ShareReportDesign_SetsTheUsersAndRoles_AndRefusesAnUnknownUser()
    {
        // Arrange
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design);

        // Act
        var refused = await InvokeAsync(ShareReportDesignTool.TheName, services, new() { ["id"] = "r1", ["users"] = new[] { "jane", "janne" } });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ShareReportDesignTool.TheName, _harness.Build(true, _design), new() { ["id"] = "r1", ["users"] = new[] { "jane" }, ["roles"] = new[] { "sales manager" } }));

        // Assert
        Assert.Contains("no user named 'janne'", refused);
        Assert.True(json.GetProperty("shared").GetBoolean());
        var saved = await _harness.Designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);
        Assert.Equal(["Jane"], saved.SharedUserNames);
        Assert.Equal(["Sales Manager"], saved.SharedRoles);
    }

    [Fact]
    public async Task ShareReportDesign_RefusesAnonymousVisitors_WithoutThePermissionToShareReportsPublicly()
    {
        // Arrange
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design);

        // Act
        var result = await InvokeAsync(ShareReportDesignTool.TheName, services, new() { ["id"] = "r1", ["roles"] = new[] { "Anonymous" } });

        // Assert
        Assert.Contains("not allowed to share reports with anonymous visitors", result);
        Assert.Empty((await _harness.Designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).SharedRoles);
    }

    [Fact]
    public async Task RestoreReportDesignVersion_PublishesTheVersion_AndKeepsTheCurrentSharing()
    {
        // Arrange: version 1 was shared with nobody; the report is now shared with Jane and renamed.
        var services = _harness.Build(true, _design);
        var created = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportDesignTool.TheName, services, new()
        {
            ["title"] = "First title",
            ["query"] = JsonSerializer.SerializeToElement(ReportToolHarness.Report().Query, CrestApps.OrchardCore.Reports.Designer.ViewModels.ReportDesignerJson.Options),
        }));
        var id = created.GetProperty("id").GetString();
        await InvokeAsync(SaveReportDesignTool.TheName, _harness.Build(true, _design), new() { ["id"] = id, ["title"] = "Second title" });
        await InvokeAsync(ShareReportDesignTool.TheName, _harness.Build(true, _design), new() { ["id"] = id, ["users"] = new[] { "jane" } });

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(RestoreReportDesignVersionTool.TheName, _harness.Build(true, _design), new() { ["id"] = id, ["version"] = 1 }));
        var missing = await InvokeAsync(RestoreReportDesignVersionTool.TheName, _harness.Build(true, _design), new() { ["id"] = id, ["version"] = 99 });

        // Assert
        Assert.True(json.GetProperty("restored").GetBoolean());
        var saved = await _harness.Designs.FindByIdAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal("First title", saved.DisplayText);
        Assert.Equal(["Jane"], saved.SharedUserNames);
        Assert.Contains("has no version 99", missing);
    }

    [Fact]
    public async Task DeleteReportDesign_DeletesTheReportAndItsShareLinks()
    {
        // Arrange
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        await _harness.SharedLinks.CreateAsync(new global::OrchardCore.DataPipelines.SharedLinks.SharedLinkRequest { Kind = ReportsConstants.SharedLinkKind, ResourceId = "r1", Recipients = ["Jane"] }, TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteReportDesignTool.TheName, services, new() { ["id"] = "r1" }));
        var missing = await InvokeAsync(DeleteReportDesignTool.TheName, _harness.Build(true, _design), new() { ["id"] = "nope" });

        // Assert
        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Null(await _harness.Designs.FindByIdAsync("r1", TestContext.Current.CancellationToken));
        Assert.Empty(_harness.SharedLinks.Links);
        Assert.Contains("No report you may open has the id 'nope'", missing);
    }

    [Fact]
    public async Task CreateReportShareLink_EmailsTheLink_AndNeverReturnsItsAddress()
    {
        // Arrange
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design, _sharePublicly);

        // Act
        var result = await InvokeAsync(CreateReportShareLinkTool.TheName, services, new() { ["id"] = "r1", ["recipients"] = new[] { "Jane" }, ["expiresAfterDays"] = 3 });
        var noRecipient = await InvokeAsync(CreateReportShareLinkTool.TheName, services, new() { ["id"] = "r1", ["recipients"] = Array.Empty<string>() });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var link = Assert.Single(_harness.SharedLinks.Links);
        Assert.Equal(link.LinkId, json.GetProperty("linkId").GetString());
        Assert.DoesNotContain("token", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/SharedLinks/", result, StringComparison.Ordinal);
        Assert.Contains("at least one recipient", noRecipient);
    }

    [Fact]
    public async Task RevokeReportShareLink_RevokesTheReportsLink_AndSaysWhenTheLinkIsUnknown()
    {
        // Arrange
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        var created = await _harness.SharedLinks.CreateAsync(new global::OrchardCore.DataPipelines.SharedLinks.SharedLinkRequest { Kind = ReportsConstants.SharedLinkKind, ResourceId = "r1", Recipients = ["Jane"] }, TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design, _sharePublicly);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(RevokeReportShareLinkTool.TheName, services, new() { ["id"] = "r1", ["linkId"] = created.Link.LinkId }));
        var unknown = await InvokeAsync(RevokeReportShareLinkTool.TheName, services, new() { ["id"] = "r1", ["linkId"] = "nope" });

        // Assert
        Assert.True(json.GetProperty("revoked").GetBoolean());
        Assert.NotNull(created.Link.RevokedUtc);
        Assert.Contains("no share link 'nope'", unknown);
    }

    [Fact]
    public async Task ListReportViews_ShowsTheQueryOnlyToThoseWhoMayChangeTheView()
    {
        // Arrange
        var services = _harness.Build(true, _design);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListReportViewsTool.TheName, services, new() { ["id"] = "v1" }));

        // Assert
        Assert.False(json.GetProperty("view").GetProperty("canEdit").GetBoolean());
        Assert.False(json.TryGetProperty("query", out _));
    }

    [Fact]
    public async Task SaveReportView_CreatesAView_AndDeleteReportView_RefusesAViewAReportReads()
    {
        // Arrange
        var services = _harness.Build(true, _design);
        var created = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportViewTool.TheName, services, new()
        {
            ["name"] = "Customer totals",
            ["query"] = JsonSerializer.SerializeToElement(ReportToolHarness.Report().Query, CrestApps.OrchardCore.Reports.Designer.ViewModels.ReportDesignerJson.Options),
        }));
        var viewId = created.GetProperty("id").GetString();
        var reader = ReportToolHarness.Report("r3", "Reads the view", ReportToolHarness.CallerId);
        reader.Query.DataSets = [new ReportDataSetReference { Alias = "v", Source = ReportsConstants.ViewsDataSource, DataSet = viewId }];
        await _harness.Designs.CreateAsync(reader, TestContext.Current.CancellationToken);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteReportViewTool.TheName, _harness.Build(true, _design), new() { ["id"] = viewId }));

        // Assert
        Assert.True(created.GetProperty("saved").GetBoolean());
        Assert.False(json.GetProperty("deleted").GetBoolean());
        Assert.Contains("Reads the view", json.GetProperty("error").GetString());
        Assert.NotNull(await _harness.Views.FindByIdAsync(viewId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefreshReportView_SaysALiveViewKeepsNoStoredRows()
    {
        // Arrange
        var view = await _harness.Views.FindByIdAsync("v1", TestContext.Current.CancellationToken);
        view.OwnerId = ReportToolHarness.CallerId;
        await _harness.Views.UpdateAsync(view, TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design);

        // Act
        var result = await InvokeAsync(RefreshReportViewTool.TheName, services, new() { ["id"] = "v1" });

        // Assert
        Assert.Contains("runs live", result);
    }

    [Fact]
    public async Task ReportGroups_AreCreatedListedAndKept_WhileTheyHoldReports()
    {
        // Arrange
        var report = ReportToolHarness.Report();
        report.GroupId = "g1";
        await _harness.Designs.UpdateAsync(report, TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _groups);

        // Act
        var created = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportGroupTool.TheName, services, new() { ["name"] = "Regional", ["parentId"] = "g1" }));
        var duplicate = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportGroupTool.TheName, services, new() { ["name"] = "regional", ["parentId"] = "g1" }));
        var list = AgentToolTestContext.ReadJson(await InvokeAsync(ListReportGroupsTool.TheName, services, new()));
        var kept = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteReportGroupTool.TheName, services, new() { ["id"] = "g1" }));

        // Assert
        Assert.Equal("Sales / Regional", created.GetProperty("path").GetString());
        Assert.False(duplicate.GetProperty("saved").GetBoolean());
        var sales = list.GetProperty("items").EnumerateArray().First(item => item.GetProperty("id").GetString() == "g1");
        Assert.Equal(1, sales.GetProperty("groupCount").GetInt32());
        Assert.Equal(1, sales.GetProperty("reportCount").GetInt32());
        Assert.False(kept.GetProperty("deleted").GetBoolean());
        Assert.NotNull(await _harness.Groups.FindByIdAsync("g1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AReportChangedEarlierInTheSameMessage_IsNotTouchedAgainUntilTheNextReply()
    {
        // Arrange: one scope, as every tool call of one chat message.
        await _harness.Designs.UpdateAsync(ReportToolHarness.Report(ownerId: ReportToolHarness.CallerId), TestContext.Current.CancellationToken);
        var services = _harness.Build(true, _design, _sharePublicly);
        await InvokeAsync(SaveReportDesignTool.TheName, services, new() { ["id"] = "r1", ["title"] = "Renamed" });

        // Act
        var result = await InvokeAsync(CreateReportShareLinkTool.TheName, services, new() { ["id"] = "r1", ["recipients"] = new[] { "Jane" } });

        // Assert
        Assert.Contains("was changed earlier in this message", result);
        Assert.Empty(_harness.SharedLinks.Links);
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, arguments);

    // Arguments that reach every tool's permission check.
    private static Dictionary<string, object> AllArguments(string id)
        => new()
        {
            ["id"] = id,
            ["source"] = "Memory",
            ["dataSet"] = "Order",
            ["query"] = JsonSerializer.SerializeToElement(ReportToolHarness.Report().Query, CrestApps.OrchardCore.Reports.Designer.ViewModels.ReportDesignerJson.Options),
            ["title"] = "Changed",
            ["name"] = "Changed",
            ["users"] = new[] { "jane" },
            ["version"] = 1,
            ["recipients"] = new[] { "jane" },
            ["linkId"] = "l1",
        };

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListReportsTool.TheName => new ListReportsTool(),
            GetReportTool.TheName => new GetReportTool(),
            RunReportTool.TheName => new RunReportTool(),
            ExportReportTool.TheName => new ExportReportTool(),
            ListReportDataSetsTool.TheName => new ListReportDataSetsTool(),
            GetReportDataSetSchemaTool.TheName => new GetReportDataSetSchemaTool(),
            QueryReportDataTool.TheName => new QueryReportDataTool(),
            SaveReportDesignTool.TheName => new SaveReportDesignTool(),
            ShareReportDesignTool.TheName => new ShareReportDesignTool(),
            RestoreReportDesignVersionTool.TheName => new RestoreReportDesignVersionTool(),
            DeleteReportDesignTool.TheName => new DeleteReportDesignTool(),
            CreateReportShareLinkTool.TheName => new CreateReportShareLinkTool(),
            RevokeReportShareLinkTool.TheName => new RevokeReportShareLinkTool(),
            ListReportViewsTool.TheName => new ListReportViewsTool(),
            SaveReportViewTool.TheName => new SaveReportViewTool(),
            RefreshReportViewTool.TheName => new RefreshReportViewTool(),
            DeleteReportViewTool.TheName => new DeleteReportViewTool(),
            ListReportGroupsTool.TheName => new ListReportGroupsTool(),
            SaveReportGroupTool.TheName => new SaveReportGroupTool(),
            DeleteReportGroupTool.TheName => new DeleteReportGroupTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName)),
        };
}
