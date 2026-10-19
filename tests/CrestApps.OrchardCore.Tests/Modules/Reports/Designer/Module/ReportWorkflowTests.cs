using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.Workflows;
using CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;
using CrestApps.OrchardCore.Reports.Models;
using Moq;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportWorkflowTests
{
    [Fact]
    public void ParseFilterValues_ReadsOneFilterPerLine_AndCollectsRepeatedFilters()
    {
        // Act
        var values = ReportWorkflowService.ParseFilterValues("created = 7\nchannel = Phone\r\nchannel = SMS\n  \nnoValue =\n= orphan\nnot a filter");

        // Assert
        Assert.Equal(["created", "channel"], values.Keys);
        Assert.Equal(["7"], values["created"]);
        Assert.Equal(["Phone", "SMS"], values["channel"]);
    }

    [Fact]
    public void ParseFilterValues_OfNothing_IsEmpty()
    {
        Assert.Empty(ReportWorkflowService.ParseFilterValues(null));
        Assert.Empty(ReportWorkflowService.ParseFilterValues(string.Empty));
    }

    [Fact]
    public void ToOutput_DescribesTheRun_WithRowsByColumnLabel()
    {
        // Arrange
        var design = new ReportDesign { ItemId = "r1", DisplayText = "Sales" };
        var run = new ReportRunResult
        {
            Columns =
            [
                new ReportResultColumn { Id = "region", Label = "Region" },
                new ReportResultColumn { Id = "total", Label = "Total", IsMeasure = true },
            ],
            Rows = [["West", 120m], ["East", 80m]],
            RowCount = 2,
        };

        // Act
        var output = ReportWorkflowService.ToOutput(design, run);

        // Assert
        Assert.Equal("r1", output["ReportId"]);
        Assert.Equal("Sales", output["Title"]);
        Assert.Equal(2, output["RowCount"]);
        Assert.Equal(["Region", "Total"], (string[])output["Columns"]);

        var rows = Assert.IsType<List<Dictionary<string, object>>>(output["Rows"]);
        Assert.Equal("West", rows[0]["Region"]);
        Assert.Equal(80m, rows[1]["Total"]);
    }

    [Fact]
    public void ToHtml_RendersMetricsAndTables_Encoded_AndLeavesChartsOut()
    {
        // Arrange
        var document = new ReportDocument();
        document.Add(ReportSection.ForMetrics(null, [new ReportMetric("Revenue", "$200")]));
        document.Add(ReportSection.ForChart("Trend", new ReportChart()));
        document.Add(ReportSection.ForTable(
            "By region",
            [new ReportColumn("Region"), new ReportColumn("Total", ReportColumnAlign.End)],
            [new ReportRow(["<West>", "120"]), new ReportRow(["Total", "200"], ReportRowKind.GrandTotal)]));

        // Act
        var html = ReportWorkflowService.ToHtml(new ReportRunResult { Document = document });

        // Assert
        Assert.Contains("Revenue", html);
        Assert.Contains("$200", html);
        Assert.Contains("By region", html);
        Assert.Contains("&lt;West&gt;", html);
        Assert.DoesNotContain("<West>", html);
        Assert.Contains("text-align:right", html);
        Assert.Contains("font-weight:600", html);
        Assert.DoesNotContain("Trend", html);
    }

    [Theory]
    [InlineData(null, "r1", true)]
    [InlineData(new string[0], "r1", true)]
    [InlineData(new[] { "r1", "r2" }, "r2", true)]
    [InlineData(new[] { "r1" }, "r2", false)]
    [InlineData(new[] { "r1" }, null, false)]
    public void ReportEvents_StartOnlyForTheChosenReports(string[] reportIds, string reportId, bool expected)
    {
        var input = new Dictionary<string, object>();

        if (reportId is not null)
        {
            input[ReportEventActivity.ReportIdInput] = reportId;
        }

        Assert.Equal(expected, ReportEventActivity.Matches(reportIds, input));
    }

    [Fact]
    public async Task WorkflowHandler_TriggersEachEvent_WithTheReportAndWhatHappened()
    {
        // Arrange
        var triggered = new List<(string Name, IDictionary<string, object> Input, string CorrelationId)>();
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager
            .Setup(manager => manager.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback((string name, IDictionary<string, object> input, string correlationId, bool _, bool _) => triggered.Add((name, input, correlationId)))
            .ReturnsAsync(Array.Empty<WorkflowExecutionContext>());
        var handler = new WorkflowReportEventHandler(workflowManager.Object);
        var design = new ReportDesign { ItemId = "r1", DisplayText = "Sales" };

        // Act
        await handler.RanAsync(new ReportEventContext { Design = design, UserName = "ada", Source = ReportEventSource.Link, RowCount = 12 });
        await handler.ExportedAsync(new ReportEventContext { Design = design, UserName = "ada", Source = ReportEventSource.Admin, Format = "csv" });
        await handler.PublishedAsync(new ReportEventContext { Design = design, UserName = "ada", VersionNumber = 3 });
        await handler.RanAsync(new ReportEventContext());

        // Assert
        Assert.Equal([nameof(ReportRunEvent), nameof(ReportExportedEvent), nameof(ReportPublishedEvent)], triggered.Select(item => item.Name));
        Assert.All(triggered, item => Assert.Equal("r1", item.CorrelationId));
        Assert.Equal("r1", triggered[0].Input[ReportEventActivity.ReportIdInput]);
        Assert.Equal("Sales", triggered[0].Input["ReportTitle"]);
        Assert.Equal("Link", triggered[0].Input["Source"]);
        Assert.Equal(12, triggered[0].Input["RowCount"]);
        Assert.Equal("csv", triggered[1].Input["Format"]);
        Assert.Equal(3, triggered[2].Input["VersionNumber"]);
    }
}
