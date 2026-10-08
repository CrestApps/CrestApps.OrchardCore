using System.Globalization;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// A localizer that returns the English source text with its arguments filled in, so tests can assert on messages.
/// </summary>
internal sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments), resourceNotFound: false);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return [];
    }
}

/// <summary>
/// A report data source over in-memory tables that records every query it receives.
/// </summary>
internal sealed class InMemoryReportDataSource : IReportDataSource
{
    private readonly Dictionary<string, (ReportDataSetSchema Schema, List<object[]> Rows)> _dataSets = new(StringComparer.Ordinal);

    public InMemoryReportDataSource(string name = "Memory")
    {
        Name = name;
    }

    public string Name { get; }

    public LocalizedString DisplayName => new(Name, Name);

    public LocalizedString Description => new(Name, Name);

    public List<ReportDataSourceQuery> Queries { get; } = [];

    public HashSet<string> DeniedDataSets { get; } = new(StringComparer.Ordinal);

    public bool ApplyConditions { get; set; }

    public InMemoryReportDataSource Add(string dataSet, IEnumerable<ReportFieldDescriptor> fields, params object[][] rows)
    {
        var schema = new ReportDataSetSchema
        {
            DataSet = new ReportDataSetDescriptor(dataSet, dataSet),
            Fields = [.. fields],
        };

        _dataSets[dataSet] = (schema, [.. rows]);

        return this;
    }

    public Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ReportDataSetDescriptor> dataSets = _dataSets
            .Where(entry => !DeniedDataSets.Contains(entry.Key))
            .Select(entry => entry.Value.Schema.DataSet)
            .ToArray();

        return Task.FromResult(dataSets);
    }

    public Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (dataSet is null || DeniedDataSets.Contains(dataSet) || !_dataSets.TryGetValue(dataSet, out var entry))
        {
            return Task.FromResult<ReportDataSetSchema>(null);
        }

        return Task.FromResult(entry.Schema);
    }

    public Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        Queries.Add(query);

        var (schema, rows) = _dataSets[query.DataSet];
        IEnumerable<object[]> selected = rows;

        if (ApplyConditions)
        {
            foreach (var condition in query.Conditions)
            {
                var index = schema.Fields.IndexOf(schema.FindField(condition.Field));

                selected = selected.Where(row => condition.Operator switch
                {
                    ReportFilterOperator.In => condition.Values.Any(value => ReportDataValues.AreEqual(row[index], value)),
                    ReportFilterOperator.GreaterThanOrEqual => ReportDataValues.Compare(row[index], condition.Values[0]) >= 0,
                    ReportFilterOperator.LessThanOrEqual => ReportDataValues.Compare(row[index], condition.Values[0]) <= 0,
                    _ => true,
                });
            }
        }

        var list = selected.ToList();

        return Task.FromResult(new ReportDataTable
        {
            Fields = schema.Fields,
            Rows = list.Take(query.MaxRows).ToList(),
            Truncated = list.Count > query.MaxRows,
        });
    }
}

/// <summary>
/// Builds the report designer services over in-memory data sources.
/// </summary>
internal static class ReportDesignerTestServices
{
    public static ReportValueFormatter Formatter { get; } = new(new PassThroughStringLocalizer<ReportValueFormatter>());

    public static ReportQueryPlanner Planner(params IReportDataSource[] dataSources)
    {
        return new ReportQueryPlanner(new ReportDataSourceManager(dataSources), new PassThroughStringLocalizer<ReportQueryPlanner>());
    }

    public static ReportQueryEngine Engine(params IReportDataSource[] dataSources)
    {
        return new ReportQueryEngine(Planner(dataSources), Formatter, new PassThroughStringLocalizer<ReportQueryEngine>());
    }

    public static ReportDesignDocumentBuilder DocumentBuilder()
    {
        return new ReportDesignDocumentBuilder(Formatter, new PassThroughStringLocalizer<ReportDesignDocumentBuilder>());
    }

    public static ReportQueryExecutionContext Context(DateTime? utcNow = null)
    {
        return new ReportQueryExecutionContext
        {
            UtcNow = utcNow ?? new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc),
        };
    }

    /// <summary>
    /// A small sales model: customers and the orders that point at them through a customer id.
    /// </summary>
    public static InMemoryReportDataSource SalesData()
    {
        return new InMemoryReportDataSource()
            .Add(
                "Customer",
                [
                    new ReportFieldDescriptor("Id", "Id", ReportDataType.Text) { IsIdentifier = true },
                    new ReportFieldDescriptor("Name", "Name", ReportDataType.Text),
                    new ReportFieldDescriptor("Region", "Region", ReportDataType.Text),
                    new ReportFieldDescriptor("Active", "Active", ReportDataType.Boolean),
                ],
                ["c1", "Acme", "West", true],
                ["c2", "Globex", "East", true],
                ["c3", "Initech", "West", false],
                ["c4", "Umbrella", "North", true])
            .Add(
                "Order",
                [
                    new ReportFieldDescriptor("Id", "Id", ReportDataType.Text) { IsIdentifier = true },
                    new ReportFieldDescriptor("CustomerId", "Customer", ReportDataType.Text) { IsIdentifier = true },
                    new ReportFieldDescriptor("Total", "Total", ReportDataType.Decimal),
                    new ReportFieldDescriptor("Quantity", "Quantity", ReportDataType.Integer),
                    new ReportFieldDescriptor("PlacedUtc", "Placed", ReportDataType.DateTime),
                ],
                ["o1", "c1", 100m, 1L, new DateTime(2026, 1, 10, 15, 0, 0, DateTimeKind.Utc)],
                ["o2", "c1", 50m, 2L, new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc)],
                ["o3", "c2", 300m, 3L, new DateTime(2026, 2, 20, 23, 30, 0, DateTimeKind.Utc)],
                ["o4", "c3", 20m, 1L, new DateTime(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc)],
                ["o5", "c9", 75m, 5L, new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)]);
    }

    public static ReportQueryDefinition CustomersWithOrders(ReportJoinType joinType = ReportJoinType.Inner)
    {
        return new ReportQueryDefinition
        {
            DataSets =
            [
                new ReportDataSetReference { Alias = "c", Source = "Memory", DataSet = "Customer" },
                new ReportDataSetReference { Alias = "o", Source = "Memory", DataSet = "Order" },
            ],
            Joins =
            [
                new ReportJoinDefinition
                {
                    Alias = "o",
                    Type = joinType,
                    Conditions = [new ReportJoinCondition { LeftField = "c.Id", RightField = "o.CustomerId" }],
                },
            ],
        };
    }

    public static ReportColumnDefinition Column(string id, string field, ReportAggregate aggregate = ReportAggregate.None, ReportFieldTransform transform = ReportFieldTransform.None)
    {
        return new ReportColumnDefinition
        {
            Id = id,
            Field = field,
            Aggregate = aggregate,
            Transform = transform,
        };
    }
}
