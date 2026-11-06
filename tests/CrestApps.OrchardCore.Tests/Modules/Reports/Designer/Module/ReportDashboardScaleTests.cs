using System.Diagnostics;
using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// A scorecard dashboard shaped like a real one: four queries over one data set of 2,000 calls, each a copy of the
/// others with the same 70 calculated fields that search the call summaries and the same viewer filters, grouped by
/// agent, queue, call, and day. It reads the data set once and computes only the calculated fields each query shows,
/// and clicking an agent filters the calls and the trend chart. The time it takes is written to the test output.
/// </summary>
public sealed class ReportDashboardScaleTests : IDisposable
{
    private const int Calls = 2_000;
    private const int Agents = 20;

    private static readonly DateTime _now = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ReportDashboardScaleTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
    }

    [Fact]
    public async Task RunAsync_AScorecardDashboard_ReadsItsDataSetOnce()
    {
        // Arrange
        var data = Transcripts();

        // Act
        var run = await ReportDesignRunnerTests.Runner(data).RunAsync(Scorecard(), filterValues: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(run.Errors);
        Assert.Empty(run.Warnings);
        Assert.Equal(Agents, run.RowCount);
        Assert.Equal(["queue", "agents", "calls", "trend"], run.Document.Sections.Select(section => section.Id));
        Assert.Single(data.Queries);
    }

    [Fact]
    public async Task RunAsync_AnAgentClicked_FiltersTheCallsAndTheTrend()
    {
        // Arrange
        var data = Transcripts();
        var options = new ReportRunOptions
        {
            VisualIds = new HashSet<string>(["calls", "trend"], StringComparer.Ordinal),
        };
        options.Selections["agents"] = ["Agent 3"];

        // Act
        var all = await ReportDesignRunnerTests.Runner(Transcripts()).RunAsync(Scorecard(), filterValues: null, TestContext.Current.CancellationToken);
        var run = await ReportDesignRunnerTests.Runner(data).RunAsync(Scorecard(), filterValues: null, period: null, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(run.Warnings);
        Assert.Equal(["calls", "trend"], run.Document.Sections.Select(section => section.Id));
        Assert.Equal(Calls / Agents, run.Document.Sections[0].Rows.Count);

        // The trend chart shows the agent's average by day, not every agent's: the same values as the calls of the
        // agent, averaged by day.
        var trend = run.Document.Sections[1].Chart;
        var allTrend = all.Document.Sections[3].Chart;
        Assert.NotEqual(Assert.Single(allTrend.Datasets).Values, Assert.Single(trend.Datasets).Values);

        var agentOnly = Scorecard();
        agentOnly.Queries[2].Query.Filters.Add(new ReportFilterDefinition { Id = "agent", Field = "t.AgentName", Operator = DataFilterOperator.Equals, Values = ["Agent 3"] });
        var expected = await ReportDesignRunnerTests.Runner(Transcripts()).RunAsync(agentOnly, filterValues: null, TestContext.Current.CancellationToken);
        Assert.Equal(expected.Document.Sections[3].Chart.Labels, trend.Labels);
        Assert.Equal(Assert.Single(expected.Document.Sections[3].Chart.Datasets).Values, Assert.Single(trend.Datasets).Values);

        // The calls and the trend share one read of the agent's calls.
        Assert.Single(data.Queries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000)]
    public async Task RunAsync_AScorecardDashboard_Timing(int readDelayMilliseconds)
    {
        // Arrange: a read of a remote source, such as Elasticsearch, takes a while before its rows arrive.
        var output = TestContext.Current.TestOutputHelper;
        var delay = TimeSpan.FromMilliseconds(readDelayMilliseconds);
        var click = new ReportRunOptions
        {
            VisualIds = new HashSet<string>(["calls", "trend"], StringComparer.Ordinal),
        };
        click.Selections["agents"] = ["Agent 3"];

        // Warm up, then keep the best of three runs.
        await ReportDesignRunnerTests.Runner(Transcripts()).RunAsync(Scorecard(), filterValues: null, TestContext.Current.CancellationToken);

        var full = TimeSpan.MaxValue;
        var clicked = TimeSpan.MaxValue;
        var reads = 0;
        var clickReads = 0;

        // Act
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var data = Transcripts();
            data.ReadDelay = delay;
            var started = Stopwatch.GetTimestamp();

            await ReportDesignRunnerTests.Runner(data).RunAsync(Scorecard(), filterValues: null, TestContext.Current.CancellationToken);
            full = Min(full, Stopwatch.GetElapsedTime(started));
            reads = data.Queries.Count;

            var clickData = Transcripts();
            clickData.ReadDelay = delay;
            started = Stopwatch.GetTimestamp();
            await ReportDesignRunnerTests.Runner(clickData).RunAsync(Scorecard(), filterValues: null, period: null, click, TestContext.Current.CancellationToken);
            clicked = Min(clicked, Stopwatch.GetElapsedTime(started));
            clickReads = clickData.Queries.Count;
        }

        // Assert
        output?.WriteLine($"Scorecard dashboard, {Calls} calls, 70 calculated fields, 4 queries, {readDelayMilliseconds} ms per data set read: full run {full.TotalMilliseconds:F0} ms ({reads} reads); agent clicked {clicked.TotalMilliseconds:F0} ms ({clickReads} reads).");
        Assert.True(full > TimeSpan.Zero);
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right)
    {
        return left < right ? left : right;
    }

    // The calls of the last 30 days: an agent, a queue, a disposition, and a summary the calculated fields search.
    internal static InMemoryReportDataSource Transcripts()
    {
        var phrases = Enumerable.Range(1, 16).Select(index => $"phrase{index:00}").ToArray();
        var rows = new object[Calls][];

        for (var index = 0; index < Calls; index++)
        {
            var words = phrases.Where((_, phrase) => (index + phrase) % 3 == 0);
            var summary = string.Join(' ', Enumerable.Repeat("The agent greeted the caller and confirmed the account details before resolving the request.", 12)) + " " + string.Join(' ', words);

            rows[index] =
            [
                $"t{index}",
                $"Agent {index % Agents}",
                $"Queue {index % 4}",
                index % 5 == 0 ? "Escalated" : "Resolved",
                _now.AddDays(-(index % 30)).AddHours(-(index % 9)),
                summary,
            ];
        }

        return new InMemoryReportDataSource().Add(
            "Transcripts",
            [
                new DataField("TranscriptId", "Transcript", DataFieldType.Text) { IsIdentifier = true },
                new DataField("AgentName", "Agent name", DataFieldType.Text),
                new DataField("Queue", "Queue", DataFieldType.Text),
                new DataField("Disposition", "Disposition", DataFieldType.Text),
                new DataField("CallDate", "Call date", DataFieldType.DateTime),
                new DataField("Summary", "Summary", DataFieldType.Text),
            ],
            rows);
    }

    internal static ReportDesign Scorecard()
    {
        return new ReportDesign
        {
            ItemId = "scorecard",
            DisplayText = "Scorecard",
            OwnerId = "owner",
            Query = Query(
                [
                    Column("c1", "t.AgentName"),
                    Column("c2", "t.TranscriptId", DataAggregate.CountDistinct),
                    Column("c3", "QAScore", DataAggregate.Average),
                    .. Enumerable.Range(1, 16).Select(index => Column($"q{index}", $"Q{index:00}", DataAggregate.Average)),
                ],
                []),
            Queries =
            [
                new ReportNamedQuery
                {
                    Id = "queue",
                    Name = "queue",
                    Query = Query([Column("k1", "t.Queue"), Column("k2", "t.TranscriptId", DataAggregate.CountDistinct), Column("k3", "QAScore", DataAggregate.Average)], []),
                },
                new ReportNamedQuery
                {
                    Id = "calls",
                    Name = "calls",
                    Query = Query(
                        [Column("d1", "CallDay"), Column("d2", "t.TranscriptId"), Column("d3", "t.AgentName"), Column("d4", "t.Disposition"), Column("d5", "QAScore", DataAggregate.Average)],
                        [new ReportSortDefinition { ColumnId = "d1", Descending = true }]),
                },
                new ReportNamedQuery
                {
                    Id = "trend",
                    Name = "trend",
                    Query = Query([Column("t1", "CallDay"), Column("t2", "QAScore", DataAggregate.Average)], [new ReportSortDefinition { ColumnId = "t1" }]),
                },
            ],
            Containers =
            [
                new ReportContainerDefinition { Id = "cq", Title = "Queue", Width = 12 },
                new ReportContainerDefinition { Id = "ca", Title = "Agent", Width = 12 },
                new ReportContainerDefinition { Id = "cc", Title = "Calls", Width = 6 },
                new ReportContainerDefinition { Id = "ct", Title = "Trend", Width = 6 },
            ],
            Visuals =
            [
                new ReportVisualDefinition { Id = "queue", QueryId = "queue", Type = ReportVisualType.Table, ContainerId = "cq" },
                new ReportVisualDefinition
                {
                    Id = "agents",
                    Type = ReportVisualType.Table,
                    ContainerId = "ca",
                    Action = new ReportVisualAction { ColumnId = "c1", Targets = [new ReportActionTarget { VisualId = "calls" }, new ReportActionTarget { VisualId = "trend" }] },
                },
                new ReportVisualDefinition { Id = "calls", QueryId = "calls", Type = ReportVisualType.Table, ContainerId = "cc" },
                new ReportVisualDefinition { Id = "trend", QueryId = "trend", Type = ReportVisualType.Chart, ChartType = ReportChartType.Line, ContainerId = "ct", CategoryColumnId = "t1", ValueColumnIds = ["t2"] },
            ],
        };
    }

    // A copy of the scorecard query: the same calculated fields and filters, with its own columns.
    private static ReportQueryDefinition Query(IList<ReportColumnDefinition> columns, IList<ReportSortDefinition> sorts)
    {
        var calculated = new List<ReportCalculatedField>
        {
            new() { Name = "CallDay", Expression = "DATETRUNC('day', [t.CallDate])" },
        };

        for (var index = 1; index <= 16; index++)
        {
            calculated.Add(new ReportCalculatedField
            {
                Name = $"Q{index:00}",
                Expression = $"IFS(CONTAINS([t.Summary], 'phrase{index:00}'), 100, CONTAINS(LOWER([t.Summary]), 'escalat'), 50, TRUE, 0)",
            });
        }

        calculated.Add(new ReportCalculatedField
        {
            Name = "QAScore",
            Expression = "(" + string.Join(" + ", Enumerable.Range(1, 16).Select(index => $"[Q{index:00}]")) + ") / 16",
        });

        for (var index = 1; index <= 52; index++)
        {
            calculated.Add(new ReportCalculatedField
            {
                Name = $"F{index:00}",
                Expression = $"IFS(FIND('phrase{index % 16 + 1:00}', [t.Summary]) > 0, UPPER(MID([t.Summary], {index}, 8)), LEN([t.Summary]) > {index * 10}, TRIM(LEFT([t.Summary], {index})), TRUE, '')",
            });
        }

        return new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "t", Source = "Memory", DataSet = "Transcripts" }],
            CalculatedFields = calculated,
            Filters =
            [
                new ReportFilterDefinition { Id = "f1", Field = "t.CallDate", Operator = DataFilterOperator.Between, Values = ["30"], Exposed = true, Label = "Call date", Control = ReportFilterControl.DateRange },
                new ReportFilterDefinition { Id = "f2", Field = "t.Disposition", Operator = DataFilterOperator.Contains },
                new ReportFilterDefinition { Id = "f3", Field = "t.Queue", Operator = DataFilterOperator.Contains, Exposed = true, Label = "Queue" },
                new ReportFilterDefinition { Id = "f4", Field = "t.AgentName", Operator = DataFilterOperator.Contains, Exposed = true, Label = "Agent Name" },
            ],
            Columns = columns,
            Sorts = sorts,
        };
    }
}
