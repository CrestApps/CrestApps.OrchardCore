using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

public sealed class ReportQueryEngineTests
{
    [Fact]
    public async Task ExecuteAsync_InnerJoinGroupedByRegion_SumsOrderTotals()
    {
        // Arrange
        var data = SalesData();
        var query = CustomersWithOrders();
        query.Columns = [Column("region", "c.Region"), Column("revenue", "o.Total", DataAggregate.Sum)];

        // Act
        var result = await Engine(data).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsAggregated);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(["East", 300m], result.Rows[0]);
        Assert.Equal(["West", 170m], result.Rows[1]);
    }

    [Theory]
    [InlineData(DataJoinType.Inner, 4)]
    [InlineData(DataJoinType.Left, 5)]
    [InlineData(DataJoinType.Right, 5)]
    [InlineData(DataJoinType.Full, 6)]
    public async Task ExecuteAsync_JoinTypes_KeepTheExpectedUnmatchedRows(DataJoinType joinType, int expectedRows)
    {
        // Arrange
        var query = CustomersWithOrders(joinType);
        query.Columns = [Column("name", "c.Name"), Column("order", "o.Id")];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedRows, result.Rows.Count);
        Assert.Equal(joinType is DataJoinType.Left or DataJoinType.Full, result.Rows.Any(row => (string)row[0] == "Umbrella" && row[1] is null));
        Assert.Equal(joinType is DataJoinType.Right or DataJoinType.Full, result.Rows.Any(row => row[0] is null && (string)row[1] == "o5"));
    }

    [Fact]
    public async Task ExecuteAsync_JoinOnTextAndNumberKeys_MatchesByText()
    {
        // Arrange
        var data = new InMemoryReportDataSource()
            .Add("A", [new DataField("Code", "Code", DataFieldType.Text)], ["7"], ["8"])
            .Add("B", [new DataField("Code", "Code", DataFieldType.Integer), new DataField("Label", "Label", DataFieldType.Text)], [7L, "seven"]);
        var query = new ReportQueryDefinition
        {
            DataSets =
            [
                new ReportDataSetReference { Alias = "a", Source = "Memory", DataSet = "A" },
                new ReportDataSetReference { Alias = "b", Source = "Memory", DataSet = "B" },
            ],
            Joins = [new ReportJoinDefinition { Alias = "b", Conditions = [new ReportJoinCondition { LeftField = "a.Code", RightField = "b.Code" }] }],
            Columns = [Column("label", "b.Label")],
        };

        // Act
        var result = await Engine(data).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("seven", Assert.Single(result.Rows)[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_TextThatDiffersOnlyInCase_GroupsAndCountsAsOneValue_WithTheSmallestSpelling(bool reversed)
    {
        // Arrange: the same rows in either order show the same spelling.
        var query = Companies();
        query.Columns = [Column("name", "c.Name"), Column("count", ReportQueryPlanner.RowCountField), Column("regions", "c.Region", DataAggregate.CountDistinct)];
        var context = Context();
        context.Groupings.Add(["name"]);
        context.Groupings.Add([]);

        // Act
        var result = await Engine(CompaniesData(reversed)).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(["ACME CORP", 2L, 1L], result.Rows[0]);
        Assert.Equal(["Globex", 1L, 1L], result.Rows[1]);
        Assert.Equal([["ACME CORP", 2L, 1L], ["Globex", 1L, 1L]], result.Regroup(["name"]).Select(group => group.Values));
        Assert.Equal([null, 3L, 2L], Assert.Single(result.Regroup([])).Values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Regroup_OfRowsThatAreNotGrouped_MergesTextThatDiffersOnlyInCase_WithTheSmallestSpelling(bool reversed)
    {
        // Arrange
        var query = Companies();
        query.Columns = [Column("name", "c.Name"), Column("employees", "c.Employees")];

        // Act
        var result = await Engine(CompaniesData(reversed)).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsAggregated);
        Assert.Equal(["ACME CORP", 15L], result.Regroup(["name"]).Single(group => (string)group.Values[0] != "Globex").Values);
    }

    [Fact]
    public async Task ExecuteAsync_CalculatedFields_EvaluatePerRowAndPerGroup()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.CalculatedFields =
        [
            new ReportCalculatedField { Name = "UnitPrice", Label = "Unit price", Expression = "[o.Total] / [o.Quantity]" },
            new ReportCalculatedField { Name = "Big", Expression = "IF([UnitPrice] >= 50, 'Big', 'Small')" },
            new ReportCalculatedField { Name = "AverageOrder", Label = "Average order", Expression = "SUM([o.Total]) / COUNTD([o.Id])" },
        ];
        query.Columns = [Column("size", "Big"), Column("avg", "AverageOrder"), Column("unit", "UnitPrice", DataAggregate.Max)];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Big", 200m, 100m], result.Rows[0]);
        Assert.Equal(["Small", 35m, 25m], result.Rows[1]);
        Assert.Equal("Average order", result.Columns[1].Label);
        Assert.True(result.Columns[1].IsMeasure);
    }

    [Fact]
    public async Task ExecuteAsync_RowCountField_CountsRowsOfEachGroup()
    {
        // Arrange
        var query = CustomersWithOrders(DataJoinType.Left);
        query.Columns = [Column("name", "c.Name"), Column("count", ReportQueryPlanner.RowCountField), Column("orders", "o.Id", DataAggregate.Count)];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        var umbrella = result.Rows.Single(row => (string)row[0] == "Umbrella");
        Assert.Equal(1L, umbrella[1]);
        Assert.Equal(0L, umbrella[2]);
        Assert.Equal(2L, result.Rows.Single(row => (string)row[0] == "Acme")[2]);
    }

    [Fact]
    public async Task ExecuteAsync_Regroup_RecomputesAveragesInsteadOfAveragingAverages()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns = [Column("region", "c.Region"), Column("name", "c.Name"), Column("avg", "o.Total", DataAggregate.Average)];

        // Act
        var context = Context();
        context.Groupings.Add(["region"]);
        context.Groupings.Add([]);
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);
        var byRegion = result.Regroup(["region"]);

        // Assert
        var west = byRegion.Single(group => (string)group.Values[0] == "West");
        Assert.Equal(170m / 3, west.Values[2]);
        Assert.Null(west.Values[1]);
        Assert.Equal(470m / 4, result.GetTotals()[2]);
    }

    [Fact]
    public async Task ExecuteAsync_FixedRowFilter_IsPushedDownToTheSourceWhenSafe()
    {
        // Arrange
        var data = SalesData();
        var query = CustomersWithOrders(DataJoinType.Left);
        query.Filters = [new ReportFilterDefinition { Id = "f1", Field = "c.Region", Operator = DataFilterOperator.Equals, Values = ["West"] }];
        query.Columns = [Column("name", "c.Name")];

        // Act
        var result = await Engine(data).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Acme", "Acme", "Initech"], result.Rows.Select(row => (string)row[0]));
        var customerQuery = data.Queries.Single(candidate => candidate.DataSet == "Customer");
        var condition = Assert.Single(customerQuery.Conditions);
        Assert.Equal(DataFilterOperator.In, condition.Operator);
        Assert.Equal("Region", condition.Field);
        Assert.Contains("Region", customerQuery.Fields);
        Assert.DoesNotContain("Active", customerQuery.Fields);
    }

    [Fact]
    public async Task ExecuteAsync_FilterOnTheNullSupplyingSideOfAnOuterJoin_IsNotPushedDown()
    {
        // Arrange
        var data = SalesData();
        data.ApplyConditions = true;
        var query = CustomersWithOrders(DataJoinType.Right);
        query.Filters =
        [
            new ReportFilterDefinition { Id = "f1", Field = "c.Region", Operator = DataFilterOperator.In, Values = ["West"] },
            new ReportFilterDefinition { Id = "f2", Field = "o.Quantity", Operator = DataFilterOperator.LessThanOrEqual, Values = ["2"] },
        ];
        query.Columns = [Column("order", "o.Id")];

        // Act
        var result = await Engine(data).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(data.Queries.Single(candidate => candidate.DataSet == "Customer").Conditions);
        Assert.Single(data.Queries.Single(candidate => candidate.DataSet == "Order").Conditions);
        Assert.Equal(["o1", "o2", "o4"], result.Rows.Select(row => (string)row[0]));
    }

    [Fact]
    public async Task ExecuteAsync_ExposedFilter_UsesTheValuesEnteredByTheViewer()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Filters = [new ReportFilterDefinition { Id = "region", Field = "c.Region", Operator = DataFilterOperator.In, Values = ["West"], Exposed = true }];
        query.Columns = [Column("name", "c.Name"), Column("total", "o.Total", DataAggregate.Sum)];
        var context = Context();
        context.FilterValues["region"] = ["East"];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Globex", 300m], Assert.Single(result.Rows));
    }

    [Fact]
    public async Task ExecuteAsync_ExposedFilterClearedByTheViewer_IsTurnedOff()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Filters = [new ReportFilterDefinition { Id = "region", Field = "c.Region", Operator = DataFilterOperator.In, Values = ["West"], Exposed = true }];
        query.Columns = [Column("name", "c.Name")];
        var context = Context();
        context.FilterValues["region"] = [];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.Rows.Count);
    }

    [Fact]
    public async Task ExecuteAsync_FixedFilter_CannotBeOverriddenByViewerValues()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Filters = [new ReportFilterDefinition { Id = "region", Field = "c.Region", Operator = DataFilterOperator.Equals, Values = ["West"] }];
        query.Columns = [Column("name", "c.Name"), Column("region", "c.Region")];
        var context = Context();
        context.FilterValues["region"] = ["East"];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(result.Rows, row => Assert.Equal("West", row[1]));
    }

    [Fact]
    public async Task ExecuteAsync_ExposedSelectFilter_ListsTheValuesLeftByFixedFilters()
    {
        // Arrange
        var query = CustomersWithOrders(DataJoinType.Left);
        query.Filters =
        [
            new ReportFilterDefinition { Id = "active", Field = "c.Active", Operator = DataFilterOperator.Equals, Values = ["true"] },
            new ReportFilterDefinition { Id = "region", Field = "c.Region", Operator = DataFilterOperator.In, Exposed = true, Control = ReportFilterControl.MultiSelect },
        ];
        query.Columns = [Column("name", "c.Name")];
        var context = Context();
        context.FilterValues["region"] = ["North"];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["East", "North", "West"], result.FilterOptions["region"].Select(option => option.Value));
        Assert.Equal("Umbrella", Assert.Single(result.Rows)[0]);
    }

    [Fact]
    public async Task ExecuteAsync_ResultFilter_FiltersAggregatedValues()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Filters = [new ReportFilterDefinition { Id = "big", Field = "total", Stage = ReportFilterStage.Result, Operator = DataFilterOperator.GreaterThan, Values = ["100"] }];
        query.Columns = [Column("name", "c.Name"), Column("total", "o.Total", DataAggregate.Sum)];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Acme", "Globex"], result.Rows.Select(row => (string)row[0]));
    }

    [Fact]
    public async Task ExecuteAsync_SortAndLimit_KeepTheTopRows()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns = [Column("name", "c.Name"), Column("total", "o.Total", DataAggregate.Sum)];
        query.Sorts = [new ReportSortDefinition { ColumnId = "total", Descending = true }];
        query.Limit = 2;

        // Act
        var context = Context();
        context.Groupings.Add([]);
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Globex", "Acme"], result.Rows.Select(row => (string)row[0]));
        Assert.Equal(450m, result.GetTotals()[1]);
    }

    [Fact]
    public async Task ExecuteAsync_DateTimeValues_AreShownInTheTenantTimeZone()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns = [Column("day", "o.PlacedUtc", transform: DataFieldTransform.Day), Column("count", ReportQueryPlanner.RowCountField)];
        query.Filters = [new ReportFilterDefinition { Id = "o3", Field = "o.Id", Operator = DataFilterOperator.Equals, Values = ["o3"] }];
        var context = Context();
        context.ToLocal = value => DateTime.SpecifyKind(value.AddHours(2), DateTimeKind.Unspecified);

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateTime(2026, 2, 21), Assert.Single(result.Rows)[0]);
    }

    [Fact]
    public async Task ExecuteAsync_DateOnlyUpperBound_KeepsTheWholeDay()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Filters = [new ReportFilterDefinition { Id = "placed", Field = "o.PlacedUtc", Operator = DataFilterOperator.Between, Values = ["2026-02-01", "2026-02-20"] }];
        query.Columns = [Column("order", "o.Id")];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["o2", "o3"], result.Rows.Select(row => (string)row[0]));
    }

    [Fact]
    public async Task ExecuteAsync_InLastDaysFilter_CountsFromTheTenantToday()
    {
        // Arrange
        var query = CustomersWithOrders(DataJoinType.Right);
        query.Filters = [new ReportFilterDefinition { Id = "recent", Field = "o.PlacedUtc", Operator = DataFilterOperator.InLastDays, Values = ["7"] }];
        query.Columns = [Column("order", "o.Id")];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("o4", Assert.Single(result.Rows)[0]);
    }

    [Fact]
    public async Task ExecuteAsync_DetailRows_KeepSourceOrderAndRegroupSumsNumbers()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns = [Column("region", "c.Region"), Column("total", "o.Total")];

        // Act
        var context = Context();
        context.Groupings.Add(["region"]);
        context.Groupings.Add([]);
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);
        var byRegion = result.Regroup(["region"]);

        // Assert
        Assert.False(result.IsAggregated);
        Assert.Equal([100m, 50m, 300m, 20m], result.Rows.Select(row => (decimal)row[1]));
        Assert.Equal(170m, byRegion.Single(group => (string)group.Values[0] == "West").Values[1]);
    }

    [Fact]
    public async Task ExecuteAsync_MeasuresWithoutDimensions_ReturnOneRowEvenWithoutData()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Filters = [new ReportFilterDefinition { Id = "none", Field = "c.Region", Operator = DataFilterOperator.Equals, Values = ["Nowhere"] }];
        query.Columns = [Column("count", ReportQueryPlanner.RowCountField), Column("total", "o.Total", DataAggregate.Sum)];

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([0L, null], Assert.Single(result.Rows));
    }

    [Fact]
    public async Task ExecuteAsync_DataSetOverTheRowLimit_WarnsThatTheDataIsIncomplete()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns = [Column("name", "c.Name")];
        var context = Context();
        context.Limits.MaxRowsPerDataSet = 2;

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Warnings, warning => warning.Contains("Only the first 2 rows of 'Customer'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_JoinsOverTheJoinedRowLimit_StopAndWarn()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Columns = [Column("name", "c.Name")];
        var context = Context();
        context.Limits.MaxJoinedRows = 2;

        // Act
        var result = await Engine(SalesData()).ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("more than 2 rows", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_DataSetTheUserCannotRead_IsRejected()
    {
        // Arrange
        var data = SalesData();
        data.DeniedDataSets.Add("Order");
        var query = CustomersWithOrders();
        query.Columns = [Column("name", "c.Name")];

        // Act
        var exception = await Assert.ThrowsAsync<ReportQueryException>(() => Engine(data).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(exception.Errors, error => error.Contains("does not exist or is not available to you", StringComparison.Ordinal));
        Assert.Empty(data.Queries);
    }

    [Fact]
    public async Task PlanAsync_InvalidQuery_ReportsEveryProblem()
    {
        // Arrange
        var query = new ReportQueryDefinition
        {
            DataSets =
            [
                new ReportDataSetReference { Alias = "c", Source = "Memory", DataSet = "Customer" },
                new ReportDataSetReference { Alias = "o", Source = "Memory", DataSet = "Order" },
            ],
            CalculatedFields =
            [
                new ReportCalculatedField { Name = "Total", Expression = "SUM([o.Total])" },
                new ReportCalculatedField { Name = "1bad", Expression = "1" },
                new ReportCalculatedField { Name = "Broken", Expression = "[c.Missing] + 1" },
            ],
            Columns =
            [
                Column("a", "c.Name", DataAggregate.Sum),
                Column("b", "Total", DataAggregate.Max),
                Column("b", "c.Region"),
                Column("c", "c.Unknown"),
                Column("d", "c.Name", transform: DataFieldTransform.Year),
            ],
            Filters = [new ReportFilterDefinition { Id = "f", Field = "Total", Operator = DataFilterOperator.GreaterThan, Values = ["1"] }],
            Sorts = [new ReportSortDefinition { ColumnId = "zzz" }],
            Limit = 0,
        };

        // Act
        var plan = await Planner(SalesData()).PlanAsync(query, new DataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(plan.IsValid);
        Assert.Contains(plan.Errors, error => error.Contains("Join the data set 'o'", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("'1bad' is invalid", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("[c.Missing] does not exist", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("cannot apply Sum to a Text field", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("already aggregated", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("unique identifier", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("unknown field 'c.Unknown'", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("transform Year", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("must filter the result", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("unknown column 'zzz'", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("at least 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_JoinThatMatchesTheWrongDataSet_IsRejected()
    {
        // Arrange
        var query = CustomersWithOrders();
        query.Joins[0].Conditions = [new ReportJoinCondition { LeftField = "o.CustomerId", RightField = "c.Id" }];
        query.Columns = [Column("name", "c.Name")];

        // Act
        var plan = await Planner(SalesData()).PlanAsync(query, new DataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(plan.Errors, error => error.Contains("must match a field of a data set listed before it", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_UnknownSource_AsksToEnableTheFeature()
    {
        // Arrange
        var query = new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "x", Source = "Elsewhere", DataSet = "Things" }],
            Columns = [Column("a", "x.Id")],
        };

        // Act
        var plan = await Planner(SalesData()).PlanAsync(query, new DataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Enable the feature", Assert.Single(plan.Errors), StringComparison.Ordinal);
    }

    private static ReportQueryDefinition Companies()
    {
        return new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "c", Source = "Memory", DataSet = "Companies" }],
        };
    }

    private static InMemoryReportDataSource CompaniesData(bool reversed)
    {
        object[][] rows =
        [
            ["Acme Corp", "East", 10L],
            ["Globex", "West", 7L],
            ["ACME CORP", "EAST", 5L],
        ];

        return new InMemoryReportDataSource()
            .Add(
                "Companies",
                [
                    new DataField("Name", "Name", DataFieldType.Text),
                    new DataField("Region", "Region", DataFieldType.Text),
                    new DataField("Employees", "Employees", DataFieldType.Integer),
                ],
                reversed ? [.. Enumerable.Reverse(rows)] : rows);
    }
}
