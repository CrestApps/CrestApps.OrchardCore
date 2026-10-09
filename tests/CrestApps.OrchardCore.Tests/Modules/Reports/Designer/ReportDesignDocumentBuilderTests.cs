using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Models;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

public sealed class ReportDesignDocumentBuilderTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ReportDesignDocumentBuilderTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
    }

    [Fact]
    public async Task Build_WithoutVisuals_ShowsATableWithATotalsRow()
    {
        // Arrange
        var result = await RevenueByRegionAndCustomer();

        // Act
        var document = DocumentBuilder().Build("Revenue", [], result);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Equal(ReportSectionKind.Table, section.Kind);
        Assert.Equal(["Region", "Customer", "Revenue"], section.Columns.Select(column => column.Label));
        Assert.Equal(ReportColumnAlign.End, section.Columns[2].Align);
        Assert.Equal(["East", "Globex", "300"], section.Rows[0].Cells);
        Assert.Equal(["Total", string.Empty, "470"], section.Rows[^1].Cells);
        Assert.Equal(ReportRowKind.GrandTotal, section.Rows[^1].Kind);
    }

    [Fact]
    public async Task Build_ChartByCategory_RegroupsTheResult()
    {
        // Arrange
        var result = await RevenueByRegionAndCustomer();
        var visual = new ReportVisualDefinition
        {
            Id = "chart",
            Type = ReportVisualType.Chart,
            ChartType = ReportChartType.Bar,
            CategoryColumnId = "region",
            ValueColumnIds = ["revenue"],
            Width = 6,
        };

        // Act
        var document = DocumentBuilder().Build("Revenue", [visual], result);

        // Assert
        var chart = Assert.Single(document.Sections).Chart;
        Assert.Equal(["East", "West"], chart.Labels);
        Assert.Equal([300d, 170d], Assert.Single(chart.Datasets).Values);
        Assert.Equal(6, document.Sections[0].Width);
    }

    [Fact]
    public async Task Build_ChartWithSeriesColumn_SpreadsValuesIntoOneDatasetPerSeries()
    {
        // Arrange
        var result = await RevenueByRegionAndCustomer();
        var visual = new ReportVisualDefinition
        {
            Id = "chart",
            Type = ReportVisualType.Chart,
            ChartType = ReportChartType.Bar,
            CategoryColumnId = "region",
            SeriesColumnId = "customer",
            ValueColumnIds = ["revenue"],
            Stacked = true,
        };

        // Act
        var document = DocumentBuilder().Build("Revenue", [visual], result);

        // Assert
        var chart = Assert.Single(document.Sections).Chart;
        Assert.True(chart.Stacked);
        Assert.Equal(["Globex", "Acme", "Initech"], chart.Datasets.Select(dataset => dataset.Label));
        Assert.Equal([0d, 150d], chart.Datasets[1].Values);
    }

    [Fact]
    public async Task Build_Metrics_ShowGrandTotals()
    {
        // Arrange
        var result = await RevenueByRegionAndCustomer();
        var visual = new ReportVisualDefinition
        {
            Id = "kpi",
            Type = ReportVisualType.Metrics,
            ValueColumnIds = ["revenue"],
        };

        // Act
        var document = DocumentBuilder().Build("Revenue", [visual], result);

        // Assert
        var metric = Assert.Single(Assert.Single(document.Sections).Metrics);
        Assert.Equal("Revenue", metric.Label);
        Assert.Equal("470", metric.Value);
    }

    [Fact]
    public async Task Build_Pivot_CrossTabsWithRowAndColumnTotals()
    {
        // Arrange
        var result = await RevenueByRegionAndCustomer();
        var visual = new ReportVisualDefinition
        {
            Id = "pivot",
            Type = ReportVisualType.Pivot,
            ColumnIds = ["customer"],
            SeriesColumnId = "region",
            ValueColumnIds = ["revenue"],
            ShowTotals = true,
        };

        // Act
        var document = DocumentBuilder().Build("Revenue", [visual], result);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Equal(["Customer", "East", "West", "Total"], section.Columns.Select(column => column.Label));
        Assert.Equal(["Acme", string.Empty, "150", "150"], section.Rows.Single(row => row.Cells[0] == "Acme").Cells);
        Assert.Equal(["Total", "300", "170", "470"], section.Rows[^1].Cells);
    }

    [Fact]
    public async Task Validate_VisualsThatReferToMissingOrTextColumns_AreRejected()
    {
        // Arrange
        var result = await RevenueByRegionAndCustomer();
        var visuals = new[]
        {
            new ReportVisualDefinition { Id = "a", Type = ReportVisualType.Chart, Title = "Broken chart", CategoryColumnId = "gone", ValueColumnIds = ["customer"] },
            new ReportVisualDefinition { Id = "b", Type = ReportVisualType.Metrics },
            new ReportVisualDefinition { Id = "b", Type = ReportVisualType.Table },
        };

        // Act
        var errors = DocumentBuilder().Validate(visuals, result.Columns);

        // Assert
        Assert.Contains(errors, error => error.Contains("'Broken chart' refers to a column that no longer exists", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("which is not a number", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("need at least one value column", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("unique identifier", StringComparison.Ordinal));
    }

    private static Task<ReportQueryResult> RevenueByRegionAndCustomer()
    {
        var query = CustomersWithOrders();
        query.Columns =
        [
            new ReportColumnDefinition { Id = "region", Field = "c.Region", Label = "Region" },
            new ReportColumnDefinition { Id = "customer", Field = "c.Name", Label = "Customer" },
            new ReportColumnDefinition { Id = "revenue", Field = "o.Total", Label = "Revenue", Aggregate = ReportAggregate.Sum, Format = "0" },
        ];

        return Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);
    }
}
