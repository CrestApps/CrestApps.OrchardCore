using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportDesignRunnerTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ReportDesignRunnerTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
    }

    [Fact]
    public async Task RunAsync_ShowsTheVisualsOfEveryQueryInTheirOrder()
    {
        // Arrange
        var design = Dashboard();

        // Act
        var run = await Runner().RunAsync(design, filterValues: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(run.Errors);
        Assert.Equal(["byCustomer", "byMonth"], run.Document.Sections.Select(section => section.Id));

        var table = run.Document.Sections[0];
        Assert.Equal(["c1", "150"], table.Rows[0].Cells);

        var chart = run.Document.Sections[1].Chart;
        Assert.Equal(3, chart.Labels.Count);
        Assert.Equal([100d, 350d, 95d], Assert.Single(chart.Datasets).Values);

        // The report's rows are those of its main query.
        Assert.Equal(4, run.RowCount);
    }

    [Fact]
    public async Task RunAsync_AViewerFilterSharedByTwoQueries_FiltersBothWithOneControl()
    {
        // Arrange
        var design = Dashboard();
        design.Query.Filters = [Placed()];
        design.Queries[0].Query.Filters = [Placed()];
        var values = new Dictionary<string, IList<string>> { ["placed"] = ["2026-02-01", "2026-02-28"] };

        // Act
        var run = await Runner().RunAsync(design, values, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("placed", Assert.Single(run.Filters).Definition.Id);
        Assert.Equal([["c1", "50"], ["c2", "300"]], run.Document.Sections[0].Rows.Select(row => row.Cells.ToArray()));
        Assert.Equal([350d], Assert.Single(run.Document.Sections[1].Chart.Datasets).Values);
    }

    [Fact]
    public async Task RunAsync_AQueryThatCannotRun_IsAWarningThatNamesIt()
    {
        // Arrange
        var design = Dashboard();
        design.Queries[0].Query.Columns.Add(Column("broken", "o.Missing"));

        // Act
        var run = await Runner().RunAsync(design, filterValues: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(run.Errors);
        Assert.Contains(run.Warnings, warning => warning.StartsWith("By month: ", StringComparison.Ordinal));
        Assert.Equal("byCustomer", Assert.Single(run.Document.Sections).Id);
    }

    [Fact]
    public async Task RunAsync_AVisualOfARemovedQuery_IsLeftOutWithAWarning()
    {
        // Arrange
        var design = Dashboard();
        design.Queries.Clear();

        // Act
        var run = await Runner().RunAsync(design, filterValues: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(run.Warnings);
        Assert.Equal("byCustomer", Assert.Single(run.Document.Sections).Id);
    }

    [Fact]
    public async Task RunAsync_ARowPicked_FiltersTheVisualsItIsLinkedTo()
    {
        // Arrange
        var design = Dashboard();
        design.Visuals[0].Action = new ReportVisualAction { ColumnId = "customer", Targets = [new ReportActionTarget { VisualId = "byMonth" }] };
        var options = Picked("byCustomer", "c1");

        // Act
        var run = await Runner().RunAsync(design, filterValues: null, period: null, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(run.Warnings);

        // The table clicked keeps all of its rows and marks the one picked.
        var table = run.Document.Sections[0];
        Assert.Equal(["c1", "c2", "c3", "c9"], table.Rows.Select(row => row.Key));
        Assert.Equal(["byMonth"], table.Selection.TargetIds);
        Assert.Equal(["c1"], table.Selection.SelectedKeys);
        Assert.Equal(["c1"], table.Selection.SelectedLabels);
        Assert.Equal(4, run.RowCount);

        // An export can say what filters the chart.
        Assert.Same(table, Assert.Single(run.Document.GetFilteringSections(run.Document.Sections[1])));
        Assert.Empty(run.Document.GetFilteringSections(table));

        // The chart shows only the customer's months.
        Assert.Equal([100d, 50d], Assert.Single(run.Document.Sections[1].Chart.Datasets).Values);
        Assert.Null(run.Document.Sections[1].Selection);
    }

    [Fact]
    public async Task RunAsync_AChartCategoryPicked_FiltersTheTableToThatPeriod()
    {
        // Arrange
        var design = Dashboard();
        design.Visuals[1].Action = new ReportVisualAction { ColumnId = "month", Targets = [new ReportActionTarget { VisualId = "byCustomer" }] };
        var unpicked = await Runner().RunAsync(design, filterValues: null, TestContext.Current.CancellationToken);

        // Act
        var run = await Runner().RunAsync(design, filterValues: null, period: null, Picked("byMonth", "2026-02-01"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["2026-01-01", "2026-02-01", "2026-03-01"], unpicked.Document.Sections[1].Chart.Keys);
        Assert.Equal([["c1", "50"], ["c2", "300"]], run.Document.Sections[0].Rows.Select(row => row.Cells.ToArray()));
        Assert.Equal(3, run.Document.Sections[1].Chart.Labels.Count);

        // The month picked is named as the chart shows it, not by its key.
        var selection = run.Document.Sections[1].Selection;
        Assert.Equal(["2026-02-01"], selection.SelectedKeys);
        Assert.Equal([run.Document.Sections[1].Chart.Labels[1]], selection.SelectedLabels);
        Assert.NotEqual("2026-02-01", selection.SelectedLabels[0]);
    }

    [Fact]
    public async Task RunAsync_RefreshingSomeVisuals_RunsOnlyTheirQueries()
    {
        // Arrange
        var design = Dashboard();
        design.Visuals[0].Action = new ReportVisualAction { ColumnId = "customer", Targets = [new ReportActionTarget { VisualId = "byMonth" }] };
        var options = Picked("byCustomer", "c2");
        options.VisualIds = new HashSet<string>(["byMonth"], StringComparer.Ordinal);

        // Act
        var run = await Runner().RunAsync(design, filterValues: null, period: null, options, TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(run.Document.Sections);
        Assert.Equal("byMonth", section.Id);
        Assert.Equal([300d], Assert.Single(section.Chart.Datasets).Values);
        Assert.Equal(0, run.RowCount);
    }

    [Fact]
    public async Task RunAsync_ALinkedVisualWhoseQueryDoesNotReadTheField_IsAWarning()
    {
        // Arrange
        var design = Dashboard();
        design.Visuals[0].Action = new ReportVisualAction
        {
            ColumnId = "customer",
            Targets = [new ReportActionTarget { VisualId = "byMonth", Field = "c.Region" }],
        };

        // Act
        var run = await Runner().RunAsync(design, filterValues: null, period: null, Picked("byCustomer", "c1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(run.Warnings);
        Assert.Equal([100d, 350d, 95d], Assert.Single(run.Document.Sections[1].Chart.Datasets).Values);
    }

    private static ReportRunOptions Picked(string visualId, params string[] keys)
    {
        var options = new ReportRunOptions();
        options.Selections[visualId] = keys;

        return options;
    }

    // Revenue by customer from the main query, and revenue by month from a second query over the same orders.
    internal static ReportDesign Dashboard()
    {
        return new ReportDesign
        {
            ItemId = "dashboard",
            DisplayText = "Dashboard",
            OwnerId = "owner",
            Query = new ReportQueryDefinition
            {
                DataSets = [Orders()],
                Columns = [Column("customer", "o.CustomerId"), Column("revenue", "o.Total", DataAggregate.Sum)],
                Sorts = [new ReportSortDefinition { ColumnId = "customer" }],
            },
            Queries =
            [
                new ReportNamedQuery
                {
                    Id = "q1",
                    Name = "By month",
                    Query = new ReportQueryDefinition
                    {
                        DataSets = [Orders()],
                        Columns = [Column("month", "o.PlacedUtc", transform: DataFieldTransform.Month), Column("revenue", "o.Total", DataAggregate.Sum)],
                        Sorts = [new ReportSortDefinition { ColumnId = "month" }],
                    },
                },
            ],
            Visuals =
            [
                new ReportVisualDefinition { Id = "byCustomer", Type = ReportVisualType.Table },
                new ReportVisualDefinition { Id = "byMonth", QueryId = "q1", Type = ReportVisualType.Chart, CategoryColumnId = "month", ValueColumnIds = ["revenue"] },
            ],
        };
    }

    internal static ReportFilterDefinition Placed()
    {
        return new ReportFilterDefinition
        {
            Id = "placed",
            Field = "o.PlacedUtc",
            Operator = DataFilterOperator.Between,
            Exposed = true,
            Label = "Placed",
            Control = ReportFilterControl.DateRange,
        };
    }

    internal static ReportDesignRunner Runner()
    {
        var data = SalesData();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var timeZone = new Mock<ITimeZone>();
        timeZone.SetupGet(value => value.TimeZoneId).Returns("UTC");

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);
        localClock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => value);

        var owner = new User { UserId = "owner", UserName = "Owner", IsEnabled = true };
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync("owner")).ReturnsAsync(owner);

        var principalFactory = new Mock<IUserClaimsPrincipalFactory<IUser>>();
        principalFactory.Setup(factory => factory.CreateAsync(owner)).ReturnsAsync(User("owner", "Owner"));

        return new ReportDesignRunner(
            Planner(data),
            Engine(data),
            DocumentBuilder(),
            new ReportExecutionContextFactory(clock.Object, localClock.Object, Options.Create(new ReportQueryLimits())),
            new ReportOwnerPrincipalResolver(userManager.Object, principalFactory.Object),
            NullLogger<ReportDesignRunner>.Instance,
            new PassThroughStringLocalizer<ReportDesignRunner>());
    }

    private static ReportDataSetReference Orders()
    {
        return new ReportDataSetReference { Alias = "o", Source = "Memory", DataSet = "Order" };
    }
}
