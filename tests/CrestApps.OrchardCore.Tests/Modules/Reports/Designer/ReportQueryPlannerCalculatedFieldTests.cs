using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// A query computes only the calculated fields its columns and filters use, and reads only the data set fields those
/// need, while every calculated field is still checked.
/// </summary>
public sealed class ReportQueryPlannerCalculatedFieldTests
{
    [Fact]
    public async Task PlanAsync_CalculatedFieldsNoColumnOrFilterUses_AreNotComputedOrRead()
    {
        // Arrange
        var query = OrdersQuery();
        query.CalculatedFields =
        [
            new ReportCalculatedField { Name = "Unused", Expression = "UPPER([o.Id])" },
            new ReportCalculatedField { Name = "Doubled", Expression = "[o.Total] * 2" },
            new ReportCalculatedField { Name = "Unused2", Expression = "[o.Quantity] + 1" },
        ];
        query.Columns = [Column("customer", "o.CustomerId"), Column("doubled", "Doubled", DataAggregate.Sum)];

        // Act
        var plan = await PlanAsync(query);

        // Assert
        Assert.Empty(plan.Errors);
        Assert.Equal(["Doubled"], plan.RowCalculations.Select(field => field.Key));
        Assert.Equal(["CustomerId", "Total"], plan.DataSets[0].UsedFields.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(3, plan.Width);

        // The designer still lists every field.
        Assert.Contains("Unused", plan.Fields.Keys);
    }

    [Fact]
    public async Task PlanAsync_ACalculatedFieldThatReadsAnother_ComputesItFirst()
    {
        // Arrange
        var query = OrdersQuery();
        query.CalculatedFields =
        [
            new ReportCalculatedField { Name = "Net", Expression = "[o.Total] - 10" },
            new ReportCalculatedField { Name = "Unused", Expression = "[o.Quantity] * 3" },
            new ReportCalculatedField { Name = "Band", Expression = "IF([Net] > 50, 'high', 'low')" },
        ];
        query.Columns = [Column("band", "Band"), Column("rows", ReportQueryPlanner.RowCountField)];
        query.Filters = [new ReportFilterDefinition { Id = "big", Field = "Net", Operator = DataFilterOperator.GreaterThan, Values = ["0"] }];

        // Act
        var result = await RunAsync(query, [], SalesData());

        // Assert
        Assert.Equal([["high", 3L], ["low", 2L]], result.Rows.OrderBy(row => (string)row[0], StringComparer.Ordinal).Select(row => row.ToArray()));
    }

    [Fact]
    public async Task PlanAsync_ACalculatedFieldNothingUses_IsStillChecked()
    {
        // Arrange
        var query = OrdersQuery();
        query.CalculatedFields = [new ReportCalculatedField { Name = "Broken", Expression = "[o.Missing] + 1" }];

        // Act
        var plan = await PlanAsync(query);

        // Assert
        Assert.Contains(plan.Errors, error => error.Contains("Broken", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_AnAggregateCalculatedFieldOverARowCalculation_ComputesTheRowCalculation()
    {
        // Arrange
        var query = OrdersQuery();
        query.CalculatedFields =
        [
            new ReportCalculatedField { Name = "Doubled", Expression = "[o.Total] * 2" },
            new ReportCalculatedField { Name = "DoubledTotal", Expression = "SUM([Doubled])" },
        ];
        query.Columns = [Column("customer", "o.CustomerId"), Column("total", "DoubledTotal")];
        query.Sorts = [new ReportSortDefinition { ColumnId = "customer" }];

        // Act
        var result = await RunAsync(query, [], SalesData());

        // Assert
        Assert.Equal(["c1", "c2", "c3", "c9"], result.Rows.Select(row => (string)row[0]));
        Assert.Equal([300m, 600m, 40m, 150m], result.Rows.Select(row => Convert.ToDecimal(row[1], System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static Task<ReportQueryPlan> PlanAsync(ReportQueryDefinition query)
    {
        return Planner(SalesData()).PlanAsync(query, Context().DataSourceContext, TestContext.Current.CancellationToken);
    }

    private static ReportQueryDefinition OrdersQuery()
    {
        return new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "o", Source = "Memory", DataSet = "Order" }],
            Columns = [Column("customer", "o.CustomerId")],
        };
    }
}
