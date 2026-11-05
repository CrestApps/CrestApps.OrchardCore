using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// A report run emits spans for itself and its phases, and metrics, under the report telemetry source and meter.
/// </summary>
public sealed class ReportTelemetryTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ReportTelemetryTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
    }

    [Fact]
    public async Task RunAsync_WithAListener_EmitsSpansAndMetricsTaggedWithTheReport()
    {
        // Arrange
        var reportId = "telemetry-" + Guid.NewGuid().ToString("n");
        var design = ReportDesignRunnerTests.Dashboard();
        design.ItemId = reportId;
        design.Visuals[0].Action = new ReportVisualAction { ColumnId = "customer", Targets = [new ReportActionTarget { VisualId = "byMonth" }] };

        var options = new ReportRunOptions { Source = "Admin" };
        options.Selections["byCustomer"] = ["c1"];

        var spans = new ConcurrentQueue<Activity>();
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ReportTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem(ReportTelemetry.ReportIdTag), reportId))
                {
                    spans.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(activities);

        var measurements = new ConcurrentQueue<(string Instrument, double Value, Dictionary<string, object> Tags)>();
        using var meters = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ReportTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var tag in tags)
            {
                values[tag.Key] = tag.Value;
            }

            if (Equals(values.GetValueOrDefault(ReportTelemetry.ReportIdTag), reportId))
            {
                measurements.Enqueue((instrument.Name, value, values));
            }
        }

        meters.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        meters.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        meters.Start();

        // Act
        var run = await ReportDesignRunnerTests.Runner().RunAsync(design, filterValues: null, period: null, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(run.Errors);

        var names = spans.Select(span => span.OperationName).ToList();
        Assert.Equal(1, names.Count(name => name == "report.run"));
        Assert.Contains("report.plan", names);
        Assert.Contains("report.query", names);
        Assert.Contains("report.read", names);
        Assert.Contains("report.render", names);

        var root = spans.Single(span => span.OperationName == "report.run");
        Assert.Equal("Admin", root.GetTagItem(ReportTelemetry.RunSourceTag));
        Assert.Equal("Dashboard", root.GetTagItem(ReportTelemetry.ReportTitleTag));
        Assert.Equal(true, root.GetTagItem(ReportTelemetry.SelectionTag));
        Assert.Equal("success", root.GetTagItem(ReportTelemetry.OutcomeTag));

        // The query spans are children of the run, one for the main query and one for the chart's.
        var queries = spans.Where(span => span.OperationName == "report.query").ToList();
        Assert.All(queries, span => Assert.Equal(root.SpanId, span.ParentSpanId));
        Assert.Equal(["", "q1"], queries.Select(span => (string)span.GetTagItem(ReportTelemetry.QueryIdTag)).Order(StringComparer.Ordinal));

        // The data set is read from the source once and shared with the chart's query.
        var reads = spans.Where(span => span.OperationName == "report.read").ToList();
        Assert.Equal(2, reads.Count);
        Assert.Single(reads, span => Equals(span.GetTagItem("crestapps.report.read_shared"), true));
        Assert.All(reads, span => Assert.Equal(5L, span.GetTagItem("crestapps.report.rows.read")));
        Assert.All(reads, span => Assert.Equal("Order", span.GetTagItem(ReportTelemetry.DataSetTag)));

        Assert.Single(measurements, measurement => measurement.Instrument == "crestapps.report.run.duration");
        Assert.Equal(2, measurements.Count(measurement => measurement.Instrument == "crestapps.report.query.duration"));
        Assert.Equal(5d, measurements.Where(measurement => measurement.Instrument == "crestapps.report.rows.read").Sum(measurement => measurement.Value));
        Assert.Single(measurements, measurement => measurement.Instrument == "crestapps.report.reads.shared");
    }

    [Fact]
    public void ActivitySource_WithoutAListener_StartsNoSpan()
    {
        // Act
        var activity = ReportTelemetry.Start("report.unheard");

        // Assert: nothing else in the test project listens to the report source.
        Assert.Null(activity);
    }
}
