using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DataSources;
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
        var query = RevenueByRegionAndCustomer();

        // Act
        var document = DocumentBuilder().Build("Revenue", [], await RunAsync(query, [], SalesData()));

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
        var query = RevenueByRegionAndCustomer();
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
        var document = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData()));

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
        var query = RevenueByRegionAndCustomer();
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
        var document = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData()));

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
        var query = RevenueByRegionAndCustomer();
        var visual = new ReportVisualDefinition
        {
            Id = "kpi",
            Type = ReportVisualType.Metrics,
            ValueColumnIds = ["revenue"],
        };

        // Act
        var document = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData()));

        // Assert
        var metric = Assert.Single(Assert.Single(document.Sections).Metrics);
        Assert.Equal("Revenue", metric.Label);
        Assert.Equal("470", metric.Value);
    }

    [Fact]
    public async Task Build_Pivot_CrossTabsWithRowAndColumnTotals()
    {
        // Arrange
        var query = RevenueByRegionAndCustomer();
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
        var document = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData()));

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
        var result = await RunAsync(RevenueByRegionAndCustomer(), [], SalesData());
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

    [Fact]
    public async Task Table_WithSubtotals_ClosesEachGroupOfTheLeadingDimensions()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns =
        [
            new ReportColumnDefinition { Id = "region", Field = "c.Region", Label = "Region" },
            new ReportColumnDefinition { Id = "customer", Field = "c.Name", Label = "Customer" },
            new ReportColumnDefinition { Id = "order", Field = "o.Id", Label = "Order" },
            new ReportColumnDefinition { Id = "revenue", Field = "o.Total", Label = "Revenue", Aggregate = DataAggregate.Sum, Format = "0" },
            new ReportColumnDefinition { Id = "average", Field = "o.Total", Label = "Average", Aggregate = DataAggregate.Average, Format = "0.00" },
        ];
        var visual = new ReportVisualDefinition { Id = "t", Type = ReportVisualType.Table, ShowTotals = true, ShowSubtotals = true };

        // Act
        var section = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData())).Sections.Single();

        // Assert
        // The region average is the average of its orders (170 / 3), not the average of its customers' averages.
        Assert.Equal(
            [
                ["East", "Globex", "o3", "300", "300.00"],
                [string.Empty, "Globex total", string.Empty, "300", "300.00"],
                ["East total", string.Empty, string.Empty, "300", "300.00"],
                ["West", "Acme", "o1", "100", "100.00"],
                ["West", "Acme", "o2", "50", "50.00"],
                [string.Empty, "Acme total", string.Empty, "150", "75.00"],
                ["West", "Initech", "o4", "20", "20.00"],
                [string.Empty, "Initech total", string.Empty, "20", "20.00"],
                ["West total", string.Empty, string.Empty, "170", "56.67"],
                ["Total", string.Empty, string.Empty, "470", "117.50"],
            ],
            section.Rows.Select(row => row.Cells.ToArray()));
        Assert.Equal(
            [ReportRowKind.Detail, ReportRowKind.Subtotal, ReportRowKind.Subtotal, ReportRowKind.Detail, ReportRowKind.Detail, ReportRowKind.Subtotal, ReportRowKind.Detail, ReportRowKind.Subtotal, ReportRowKind.Subtotal, ReportRowKind.GrandTotal],
            section.Rows.Select(row => row.Kind));
    }

    [Fact]
    public async Task Table_WithSubtotals_KeepsGroupsTogetherWhenSortedByAMeasure()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns =
        [
            new ReportColumnDefinition { Id = "region", Field = "c.Region", Label = "Region" },
            new ReportColumnDefinition { Id = "customer", Field = "c.Name", Label = "Customer" },
            new ReportColumnDefinition { Id = "revenue", Field = "o.Total", Label = "Revenue", Aggregate = DataAggregate.Sum, Format = "0" },
        ];
        query.Sorts = [new ReportSortDefinition { ColumnId = "revenue", Descending = true }];
        var visual = new ReportVisualDefinition { Id = "t", Type = ReportVisualType.Table, ShowSubtotals = true };

        // Act
        var section = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData())).Sections.Single();

        // Assert: East comes first (its first row is the largest), West's customers stay in revenue order.
        Assert.Equal(
            ["Globex", "East total", "Acme", "Initech", "West total"],
            section.Rows.Select(row => row.Cells[1].Length > 0 ? row.Cells[1] : row.Cells[0]));
    }

    [Fact]
    public async Task Table_ListingSomeDimensions_GroupsByThemWithTotals()
    {
        // Arrange
        var query = RevenueByRegionAndCustomer();
        var visual = new ReportVisualDefinition { Id = "t", Type = ReportVisualType.Table, ColumnIds = ["region", "revenue"], ShowTotals = true };

        // Act
        var section = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData())).Sections.Single();

        // Assert: one row per region, not one per region and customer.
        Assert.Equal(["Region", "Revenue"], section.Columns.Select(column => column.Label));
        Assert.Equal(
            [
                ["East", "300"],
                ["West", "170"],
                ["Total", "470"],
            ],
            section.Rows.Select(row => row.Cells.ToArray()));
    }

    [Fact]
    public async Task Table_ListingSomeDimensions_ComputesAveragesOverTheUnderlyingRows()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns =
        [
            new ReportColumnDefinition { Id = "region", Field = "c.Region", Label = "Region" },
            new ReportColumnDefinition { Id = "customer", Field = "c.Name", Label = "Customer" },
            new ReportColumnDefinition { Id = "average", Field = "o.Total", Label = "Average", Aggregate = DataAggregate.Average, Format = "0.00" },
        ];
        var regions = new ReportVisualDefinition { Id = "regions", Type = ReportVisualType.Table, ColumnIds = ["region", "average"] };
        var customers = new ReportVisualDefinition { Id = "customers", Type = ReportVisualType.Table };
        var visuals = new[] { regions, customers };

        // Act
        var document = DocumentBuilder().Build("Revenue", visuals, await RunAsync(query, visuals, SalesData()));

        // Assert: West averages its three orders (170 / 3), not its two customers' averages; the other table is unchanged.
        Assert.Equal(
            [
                ["East", "300.00"],
                ["West", "56.67"],
            ],
            document.Sections[0].Rows.Select(row => row.Cells.ToArray()));
        Assert.Equal(3, document.Sections[1].Rows.Count);
    }

    [Fact]
    public async Task Table_ListingSomeDimensions_StillSupportsSubtotals()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns =
        [
            new ReportColumnDefinition { Id = "region", Field = "c.Region", Label = "Region" },
            new ReportColumnDefinition { Id = "customer", Field = "c.Name", Label = "Customer" },
            new ReportColumnDefinition { Id = "order", Field = "o.Id", Label = "Order" },
            new ReportColumnDefinition { Id = "revenue", Field = "o.Total", Label = "Revenue", Aggregate = DataAggregate.Sum, Format = "0" },
        ];
        var visual = new ReportVisualDefinition { Id = "t", Type = ReportVisualType.Table, ColumnIds = ["region", "customer", "revenue"], ShowSubtotals = true };

        // Act
        var section = DocumentBuilder().Build("Revenue", [visual], await RunAsync(query, [visual], SalesData())).Sections.Single();

        // Assert
        Assert.Equal(
            [
                ["East", "Globex", "300"],
                ["East total", string.Empty, "300"],
                ["West", "Acme", "150"],
                ["West", "Initech", "20"],
                ["West total", string.Empty, "170"],
            ],
            section.Rows.Select(row => row.Cells.ToArray()));
    }

    [Fact]
    public async Task AddContainers_PlacesContainersInTheOrderOfTheirFirstSections()
    {
        // Arrange
        var query = RevenueByRegionAndCustomer();
        ReportVisualDefinition[] visuals =
        [
            new() { Id = "totals", Type = ReportVisualType.Metrics, ValueColumnIds = ["revenue"] },
            new() { Id = "table", Type = ReportVisualType.Table, ContainerId = "detail" },
            new() { Id = "chart", Type = ReportVisualType.Chart, CategoryColumnId = "region", ValueColumnIds = ["revenue"], ContainerId = "trend" },
            new() { Id = "second", Type = ReportVisualType.Table, ContainerId = "detail" },
        ];
        var document = DocumentBuilder().Build("Revenue", visuals, await RunAsync(query, visuals, SalesData()));

        // Act
        ReportDesignDocumentBuilder.AddContainers(document,
        [
            new ReportContainerDefinition { Id = "trend", Title = "Trend", Width = 6, Height = 9000 },
            new ReportContainerDefinition { Id = "detail", Title = "Detail", Width = 6, Height = 320 },
            new ReportContainerDefinition { Id = "unused", Title = "Unused" },
        ]);

        // Assert
        Assert.Equal(["detail", "trend"], document.Containers.Select(container => container.Id));
        Assert.Equal(320, document.Containers[0].Height);
        Assert.Equal(ReportContainerDefinition.MaxHeight, document.Containers[1].Height);
        Assert.Equal(["totals", "table", "chart", "second"], document.Sections.Select(section => section.Id));
        Assert.Equal([null, "detail", "trend", "detail"], document.Sections.Select(section => section.ContainerId));
    }

    [Fact]
    public async Task AddContainers_ShowsASectionOfAMissingContainerOnItsOwn()
    {
        // Arrange
        var query = RevenueByRegionAndCustomer();
        ReportVisualDefinition[] visuals = [new() { Id = "table", Type = ReportVisualType.Table, ContainerId = "gone" }];
        var document = DocumentBuilder().Build("Revenue", visuals, await RunAsync(query, visuals, SalesData()));

        // Act
        ReportDesignDocumentBuilder.AddContainers(document, []);

        // Assert
        Assert.Empty(document.Containers);
        Assert.Null(Assert.Single(document.Sections).ContainerId);
    }

    private static ReportQueryDefinition RevenueByRegionAndCustomer()
    {
        var query = CustomersWithOrders();
        query.Columns =
        [
            new ReportColumnDefinition { Id = "region", Field = "c.Region", Label = "Region" },
            new ReportColumnDefinition { Id = "customer", Field = "c.Name", Label = "Customer" },
            new ReportColumnDefinition { Id = "revenue", Field = "o.Total", Label = "Revenue", Aggregate = DataAggregate.Sum, Format = "0" },
        ];

        return query;
    }
}
