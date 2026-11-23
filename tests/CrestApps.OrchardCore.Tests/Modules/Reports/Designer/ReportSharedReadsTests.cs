using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// The queries of one report run share reads of a data set: a kept read serves a query with the same conditions, and
/// one with more conditions (the engine filters again), unless the data source says its data set decides which rows to
/// release from the read's own conditions.
/// </summary>
public sealed class ReportSharedReadsTests
{
    [Fact]
    public async Task ANarrowerQuery_IsServedFromTheKeptRead_ByDefault()
    {
        // Arrange
        var source = new CountingDataSource(policy: null);
        var shared = Shared(source, out var dataSet);

        // Act
        await ReadAllAsync(shared, dataSet, Query());
        await ReadAllAsync(shared, dataSet, Query(Condition("Department", "Legal")));

        // Assert
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, shared.SharedCount);
    }

    [Fact]
    public async Task ANarrowerQuery_ReachesTheDataSource_WhenTheDataSetGatesOnItsConditions()
    {
        // Arrange: two views of one report, the second filtered (a dashboard click on Legal).
        var source = new CountingDataSource(policy: false);
        var shared = Shared(source, out var dataSet);

        // Act
        await ReadAllAsync(shared, dataSet, Query());
        await ReadAllAsync(shared, dataSet, Query(Condition("Department", "Legal")));

        // Assert: the filtered view reached the data set with its own conditions.
        Assert.Equal(2, source.Reads);
        Assert.Equal(0, shared.SharedCount);
        Assert.Single(source.LastConditions);
    }

    [Fact]
    public async Task TheSameConditions_AreStillShared_WhenTheDataSetGatesOnItsConditions()
    {
        // Arrange
        var source = new CountingDataSource(policy: false);
        var shared = Shared(source, out var dataSet);

        // Act
        await ReadAllAsync(shared, dataSet, Query(Condition("Department", "Legal")));
        await ReadAllAsync(shared, dataSet, Query(Condition("Department", "Legal")));

        // Assert
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, shared.SharedCount);
    }

    private static ReportSharedReads Shared(CountingDataSource source, out PlannedDataSet dataSet)
    {
        dataSet = new PlannedDataSet
        {
            Reference = new ReportDataSetReference { Alias = "r", Source = "Counting", DataSet = "Responses" },
            Source = source,
        };

        var shared = new ReportSharedReads(1000);

        for (var i = 0; i < 2; i++)
        {
            var plan = new ReportQueryPlan();
            plan.DataSets.Add(dataSet);
            shared.Register(plan);
        }

        return shared;
    }

    private static DataSourceQuery Query(params DataCondition[] conditions)
    {
        var query = new DataSourceQuery
        {
            DataSet = "Responses",
            Fields = new HashSet<string>(["Department"], StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext(),
        };

        foreach (var condition in conditions)
        {
            query.Conditions.Add(condition);
        }

        return query;
    }

    private static DataCondition Condition(string field, string value)
        => new() { Field = field, Operator = DataFilterOperator.Equals, Values = [value] };

    private static async Task ReadAllAsync(ReportSharedReads shared, PlannedDataSet dataSet, DataSourceQuery query)
    {
        await foreach (var _ in shared.ReadAsync(dataSet, query, TestContext.Current.CancellationToken))
        {
        }
    }

    // A data source that counts its reads, optionally implementing the shared read policy.
    private sealed class CountingDataSource : IDataSource, IReportSharedReadPolicy
    {
        private static readonly DataField[] _fields = [new("Department", "Department", DataFieldType.Text)];
        private readonly bool? _policy;

        public CountingDataSource(bool? policy)
        {
            _policy = policy;
        }

        public static string Name => "Counting";

        public static global::OrchardCore.Localization.LocalizationSource DisplayName { get; } = global::OrchardCore.Localization.LocalizationSource.Create("Counting");

        public static global::OrchardCore.Localization.LocalizationSource Description { get; } = global::OrchardCore.Localization.LocalizationSource.Create("Counting");

        public int Reads { get; private set; }

        public IReadOnlyList<DataCondition> LastConditions { get; private set; } = [];

        public bool ServesNarrowerReads(string dataSet)
            => _policy ?? true;

        public Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DataSetDescriptor>>([new DataSetDescriptor("Responses", "Responses")]);

        public Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new DataSetSchema { DataSet = new DataSetDescriptor("Responses", "Responses"), Fields = _fields });

        public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            Reads++;
            LastConditions = [.. query.Conditions];

            yield return new DataBatch(_fields, [["Legal"], ["Sales"], ["Sales"]]);
        }
    }
}
