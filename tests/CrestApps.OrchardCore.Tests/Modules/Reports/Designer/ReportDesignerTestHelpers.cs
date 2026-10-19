using System.Globalization;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.DataSources.Operations;
using OrchardCore.Localization;

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
/// A localizer factory whose localizers return the English source text.
/// </summary>
internal sealed class PassThroughStringLocalizerFactory : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource)
    {
        return new PassThroughStringLocalizer<object>();
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        return new PassThroughStringLocalizer<object>();
    }
}

/// <summary>
/// A data source a test resolves by a name of its choice, rather than by the static name of the data source's class.
/// </summary>
internal sealed class TestDataSourceEntry
{
    public TestDataSourceEntry(string name, IDataSource dataSource)
        : this(name, dataSource.GetType(), () => dataSource)
    {
    }

    // The data source is resolved when it is used, like Orchard Core does, so it may depend on the manager.
    public TestDataSourceEntry(string name, Type type, Func<object> resolve)
    {
        Name = name;
        Type = type;
        Resolve = resolve;
    }

    public string Name { get; }

    public Type Type { get; }

    public Func<object> Resolve { get; }
}

/// <summary>
/// Resolves the data sources of a test by the names the test gave them.
/// </summary>
internal sealed class TestDataSourceManager : IDataSourceManager
{
    private readonly Dictionary<string, (DataSourceDescriptor Descriptor, TestDataSourceEntry Entry)> _dataSources = new(StringComparer.OrdinalIgnoreCase);

    public TestDataSourceManager(IEnumerable<TestDataSourceEntry> dataSources)
    {
        foreach (var entry in dataSources)
        {
            var label = LocalizationSource.Create(entry.Name);

            _dataSources[entry.Name] = (new DataSourceDescriptor(entry.Name, label, label, entry.Type), entry);
        }
    }

    public IReadOnlyList<DataSourceDescriptor> GetDataSources()
    {
        return _dataSources.Values.Select(entry => entry.Descriptor).ToArray();
    }

    public DataSourceDescriptor GetDescriptor(string name)
    {
        return name is not null && _dataSources.TryGetValue(name, out var entry) ? entry.Descriptor : null;
    }

    public IDataSource GetDataSource(string name)
    {
        return name is not null && _dataSources.TryGetValue(name, out var entry) ? (IDataSource)entry.Entry.Resolve() : null;
    }
}

/// <summary>
/// A data source over in-memory tables that records every query it receives.
/// </summary>
internal sealed class InMemoryReportDataSource : IDataSource
{
    private readonly Dictionary<string, (DataSetSchema Schema, List<object[]> Rows)> _dataSets = new(StringComparer.Ordinal);

    public InMemoryReportDataSource(string sourceName = "Memory")
    {
        SourceName = sourceName;
    }

    public static string Name => "Memory";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create("Memory");

    public static LocalizationSource Description { get; } = LocalizationSource.Create("Memory");

    /// <summary>
    /// Gets the name the test resolves the data source by.
    /// </summary>
    public string SourceName { get; }

    public List<DataSourceQuery> Queries { get; } = [];

    public HashSet<string> DeniedDataSets { get; } = new(StringComparer.Ordinal);

    public bool ApplyConditions { get; set; }

    public InMemoryReportDataSource Add(string dataSet, IEnumerable<DataField> fields, params object[][] rows)
    {
        var schema = new DataSetSchema
        {
            DataSet = new DataSetDescriptor(dataSet, dataSet),
            Fields = [.. fields],
        };

        _dataSets[dataSet] = (schema, [.. rows]);

        return this;
    }

    public (DataSetSchema Schema, List<object[]> Rows) Table(string dataSet)
    {
        return _dataSets[dataSet];
    }

    public Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DataSetDescriptor> dataSets = _dataSets
            .Where(entry => !DeniedDataSets.Contains(entry.Key))
            .Select(entry => entry.Value.Schema.DataSet)
            .ToArray();

        return Task.FromResult(dataSets);
    }

    public Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (dataSet is null || DeniedDataSets.Contains(dataSet) || !_dataSets.TryGetValue(dataSet, out var entry))
        {
            return Task.FromResult<DataSetSchema>(null);
        }

        return Task.FromResult(entry.Schema);
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

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
                    DataFilterOperator.In => condition.Values.Any(value => DataValues.AreEqual(row[index], value)),
                    DataFilterOperator.GreaterThanOrEqual => DataValues.Compare(row[index], condition.Values[0]) >= 0,
                    DataFilterOperator.LessThanOrEqual => DataValues.Compare(row[index], condition.Values[0]) <= 0,
                    _ => true,
                });
            }
        }

        var kept = (query.MaxRows > 0 ? selected.Take(query.MaxRows) : selected).ToList();

        yield return new DataBatch(schema.Fields.ToArray(), kept);
    }
}

/// <summary>
/// Builds the report builder services over in-memory data sources.
/// </summary>
internal static class ReportDesignerTestServices
{
    public static ReportValueFormatter Formatter { get; } = new(new PassThroughStringLocalizer<ReportValueFormatter>());

    public static ReportQueryPlanner Planner(params InMemoryReportDataSource[] dataSources)
    {
        return Planner(dataSources.Select(dataSource => new TestDataSourceEntry(dataSource.SourceName, dataSource)).ToArray());
    }

    public static ReportQueryPlanner Planner(params TestDataSourceEntry[] dataSources)
    {
        return new ReportQueryPlanner(
            new TestDataSourceManager(dataSources),
            ExpressionFunctionCatalog.BuiltIn,
            new PassThroughStringLocalizerFactory(),
            new PassThroughStringLocalizer<ReportQueryPlanner>());
    }

    public static ReportQueryEngine Engine(params InMemoryReportDataSource[] dataSources)
    {
        return new ReportQueryEngine(Planner(dataSources), Formatter, new ReportQueryGate(), new PassThroughStringLocalizer<ReportQueryEngine>());
    }

    public static ReportQueryEngine Engine(params TestDataSourceEntry[] dataSources)
    {
        return new ReportQueryEngine(Planner(dataSources), Formatter, new ReportQueryGate(), new PassThroughStringLocalizer<ReportQueryEngine>());
    }

    /// <summary>
    /// Runs a query like the report runner does: the groupings the visuals regroup the result by, such as totals and
    /// subtotals, are computed while the query runs.
    /// </summary>
    public static async Task<ReportQueryResult> RunAsync(ReportQueryDefinition query, IEnumerable<ReportVisualDefinition> visuals, params InMemoryReportDataSource[] dataSources)
    {
        var context = Context();
        var plan = await Planner(dataSources).PlanAsync(query, context.DataSourceContext, TestContext.Current.CancellationToken);

        foreach (var grouping in ReportDesignDocumentBuilder.GetGroupings(visuals, ReportQueryEngine.DescribeColumns(plan)))
        {
            context.Groupings.Add(grouping);
        }

        return await Engine(dataSources).ExecuteAsync(plan, context, TestContext.Current.CancellationToken);
    }

    public static ReportDesignDocumentBuilder DocumentBuilder()
    {
        return new ReportDesignDocumentBuilder(Formatter, new PassThroughStringLocalizer<ReportDesignDocumentBuilder>());
    }

    /// <summary>
    /// Reads the rows a query asks a data source for, within the query's row limit.
    /// </summary>
    public static Task<DataRowSet> ReadAsync(IDataSource dataSource, DataSourceQuery query, CancellationToken cancellationToken = default)
    {
        return dataSource.ReadRowsAsync(query, query.MaxRows > 0 ? query.MaxRows : int.MaxValue, cancellationToken);
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
                    new DataField("Id", "Id", DataFieldType.Text) { IsIdentifier = true },
                    new DataField("Name", "Name", DataFieldType.Text),
                    new DataField("Region", "Region", DataFieldType.Text),
                    new DataField("Active", "Active", DataFieldType.Boolean),
                ],
                ["c1", "Acme", "West", true],
                ["c2", "Globex", "East", true],
                ["c3", "Initech", "West", false],
                ["c4", "Umbrella", "North", true])
            .Add(
                "Order",
                [
                    new DataField("Id", "Id", DataFieldType.Text) { IsIdentifier = true },
                    new DataField("CustomerId", "Customer", DataFieldType.Text) { IsIdentifier = true },
                    new DataField("Total", "Total", DataFieldType.Decimal),
                    new DataField("Quantity", "Quantity", DataFieldType.Integer),
                    new DataField("PlacedUtc", "Placed", DataFieldType.DateTime),
                ],
                ["o1", "c1", 100m, 1L, new DateTime(2026, 1, 10, 15, 0, 0, DateTimeKind.Utc)],
                ["o2", "c1", 50m, 2L, new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc)],
                ["o3", "c2", 300m, 3L, new DateTime(2026, 2, 20, 23, 30, 0, DateTimeKind.Utc)],
                ["o4", "c3", 20m, 1L, new DateTime(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc)],
                ["o5", "c9", 75m, 5L, new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)]);
    }

    public static ReportQueryDefinition CustomersWithOrders(DataJoinType joinType = DataJoinType.Inner)
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

    public static ReportColumnDefinition Column(string id, string field, DataAggregate aggregate = DataAggregate.None, DataFieldTransform transform = DataFieldTransform.None)
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
