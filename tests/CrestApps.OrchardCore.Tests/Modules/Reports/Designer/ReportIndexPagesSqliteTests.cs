using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// Data sets read their records through a real YesSql store a page at a time, newest first: every record once,
/// whatever the dates they share across pages, and only the records that the conditions of the query allow.
/// </summary>
public sealed class ReportIndexPagesSqliteTests : IAsyncLifetime
{
    private static readonly DateTime _start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-report-pages-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new PageRecordIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<PageRecordIndex>(table => table
            .Column<string>(nameof(PageRecordIndex.Name))
            .Column<string>(nameof(PageRecordIndex.Channel))
            .Column<int>(nameof(PageRecordIndex.Status))
            .Column<DateTime>(nameof(PageRecordIndex.CreatedUtc))
            .Column<DateTime?>(nameof(PageRecordIndex.ClosedUtc), column => column.Nullable()));

        await schemaBuilder.AlterIndexTableAsync<PageRecordIndex>(table => table
            .CreateIndex("IDX_PageRecordIndex_Report", nameof(PageRecordIndex.CreatedUtc), "Id"));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReadsEveryRecordOnceNewestFirst_WhenManyShareADateAcrossPages()
    {
        // Three dates for more than two pages of records, so pages end and start among records of the same date.
        await AddAsync(Enumerable.Range(0, (ReportIndexPages.PageSize * 2) + 201)
            .Select(number => new PageRecord { Name = $"r{number}", CreatedUtc = _start.AddDays(number % 3) }));

        var names = await ReadAsync(null);

        Assert.Equal((ReportIndexPages.PageSize * 2) + 201, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());

        // Newest first: the records of the last day, then of the day before, each day by the order they were added
        // in reverse.
        var expected = Enumerable.Range(0, (ReportIndexPages.PageSize * 2) + 201)
            .OrderByDescending(number => number % 3)
            .ThenByDescending(number => number)
            .Select(number => $"r{number}");

        Assert.Equal(expected, names);
    }

    [Fact]
    public async Task ReadsTheRecordsWithoutADateLast()
    {
        await AddAsync(Enumerable.Range(0, ReportIndexPages.PageSize + 10)
            .Select(number => new PageRecord
            {
                Name = $"r{number}",
                CreatedUtc = _start,
                ClosedUtc = number % 2 == 0 ? _start.AddHours(number % 5) : null,
            }));

        var names = new List<string>();

        await using (var session = _store.CreateSession())
        {
            await foreach (var record in ReportIndexPages.ReadAsync<PageRecord, PageRecordIndex>(
                session, null, index => index.ClosedUtc, cancellationToken: TestContext.Current.CancellationToken))
            {
                names.Add(record.Name);
            }
        }

        Assert.Equal(ReportIndexPages.PageSize + 10, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names.Take((ReportIndexPages.PageSize + 10) / 2), name => Assert.True(int.Parse(name[1..]) % 2 == 0));
        Assert.All(names.Skip((ReportIndexPages.PageSize + 10) / 2), name => Assert.True(int.Parse(name[1..]) % 2 == 1));
    }

    [Fact]
    public async Task ReadsOnlyTheRecordsTheFilterAllows()
    {
        await AddAsync(Enumerable.Range(0, ReportIndexPages.PageSize + 100)
            .Select(number => new PageRecord { Name = $"r{number}", Channel = number % 4 == 0 ? "SMS" : "Phone", CreatedUtc = _start.AddMinutes(number) }));

        var names = await ReadAsync(rows => rows.Where(index => index.Channel == "SMS"));

        Assert.Equal((ReportIndexPages.PageSize + 100) / 4, names.Count);
    }

    [Fact]
    public async Task ListConditions_CompareTextWithoutRegardToCase_AndEnumerationsByName()
    {
        await AddAsync(Enumerable.Range(0, 40)
            .Select(number => new PageRecord
            {
                Name = $"r{number}",
                Channel = number % 2 == 0 ? "SMS" : "Phone",
                Status = (PageStatus)(number % 4),
                CreatedUtc = _start.AddMinutes(number),
            }));

        var conditions = new ReportIndexConditions<PageRecordIndex>(
        [
            Condition("Channel", "sms"),
            Condition("Status", "closed", "FAILED"),
        ])
            .Text("Channel", nameof(PageRecordIndex.Channel))
            .Enum("Status", index => index.Status);

        var names = await ReadAsync(conditions.Apply);

        Assert.False(conditions.MatchesNothing);
        Assert.Equal(10, names.Count);
        Assert.All(names, name =>
        {
            var number = int.Parse(name[1..]);

            Assert.True(number % 2 == 0);
            Assert.Equal(PageStatus.Closed, (PageStatus)(number % 4));
        });
    }

    [Fact]
    public async Task ListConditions_LeaveTextThatIsNotAsciiToTheConsumer()
    {
        await AddAsync([new PageRecord { Name = "a", Channel = "Électronique", CreatedUtc = _start }, new PageRecord { Name = "b", Channel = "Phone", CreatedUtc = _start }]);

        var conditions = new ReportIndexConditions<PageRecordIndex>([Condition("Channel", "électronique")])
            .Text("Channel", nameof(PageRecordIndex.Channel));

        // The database can't compare the value without regard to case the way the consumer does, so it reads every
        // record and the consumer filters them.
        Assert.Equal(2, (await ReadAsync(conditions.Apply)).Count);
    }

    [Fact]
    public void ListConditions_MatchNothing_WhenNoValueIsANameOfTheEnumeration()
    {
        var conditions = new ReportIndexConditions<PageRecordIndex>([Condition("Status", "Archived")])
            .Enum("Status", index => index.Status);

        Assert.True(conditions.MatchesNothing);
    }

    private static DataCondition Condition(string field, params string[] values)
    {
        return new DataCondition
        {
            Field = field,
            Operator = DataFilterOperator.In,
            Values = [.. values],
        };
    }

    private async Task AddAsync(IEnumerable<PageRecord> records)
    {
        await using var session = _store.CreateSession();

        foreach (var record in records)
        {
            await session.SaveAsync(record, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<string>> ReadAsync(Func<IQueryIndex<PageRecordIndex>, IQueryIndex<PageRecordIndex>> filter)
    {
        var names = new List<string>();

        await using var session = _store.CreateSession();

        await foreach (var page in ReportIndexPages.ReadPagesAsync<PageRecord, PageRecordIndex>(
            session, filter, index => index.CreatedUtc, cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.True(page.Count <= ReportIndexPages.PageSize);

            names.AddRange(page.Select(record => record.Name));
        }

        return names;
    }

    public enum PageStatus
    {
        Open,
        Pending,
        Closed,
        Failed,
    }

    public sealed class PageRecord
    {
        public string Name { get; set; }

        public string Channel { get; set; }

        public PageStatus Status { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? ClosedUtc { get; set; }
    }

    public sealed class PageRecordIndex : MapIndex
    {
        public string Name { get; set; }

        public string Channel { get; set; }

        public PageStatus Status { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? ClosedUtc { get; set; }
    }

    private sealed class PageRecordIndexProvider : IndexProvider<PageRecord>
    {
        public override void Describe(DescribeContext<PageRecord> context)
        {
            context.For<PageRecordIndex>().Map(record => new PageRecordIndex
            {
                Name = record.Name,
                Channel = record.Channel,
                Status = record.Status,
                CreatedUtc = record.CreatedUtc,
                ClosedUtc = record.ClosedUtc,
            });
        }
    }
}
