using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Telemetry.Reports;

/// <summary>
/// The tests that listen to the report source and meter. They run on their own, so a test that expects no listener on
/// the report source, and the measurements each test filters, are not disturbed by another test running at the same
/// time.
/// </summary>
[CollectionDefinition(nameof(ReportTelemetryCollection), DisableParallelization = true)]
public sealed class ReportTelemetryCollection;

/// <summary>
/// The instrument and span names of coded reports, exports, views, subscriptions and the designer are a public contract:
/// they are emitted with bounded tags only.
/// </summary>
[Collection(nameof(ReportTelemetryCollection))]
public sealed class ReportTelemetryContractTests
{
    [Fact]
    public async Task RunCodedReportAsync_RecordsTheRunByRegisteredNameAndMode()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var name = "coded-" + Guid.NewGuid().ToString("n");
        var descriptor = Descriptor(name);
        var document = new ReportDocument().Add(ReportSection.ForTable("Rows", [], []));

        // Act
        var result = await ReportTelemetry.RunCodedReportAsync(new FixedReport(document), descriptor, new ReportContext(new ReportFilter(), ReportRunMode.Export), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(document, result);

        var run = Assert.Single(capture.Find("crestapps.report.coded.duration", ReportTelemetry.ReportNameTag, name));
        Assert.Equal("export", run.Tags[ReportTelemetry.ModeTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, run.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Contains(capture.Spans, span => span.OperationName == "report.coded.run" && Equals(span.GetTagItem(ReportTelemetry.ReportNameTag), name));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task RunCodedReportAsync_WhenTheReportThrows_RecordsAnError()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var name = "failing-" + Guid.NewGuid().ToString("n");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReportTelemetry.RunCodedReportAsync(new FixedReport(null), Descriptor(name), new ReportContext(new ReportFilter()), TestContext.Current.CancellationToken));

        // Assert
        var run = Assert.Single(capture.Find("crestapps.report.coded.duration", ReportTelemetry.ReportNameTag, name));
        Assert.Equal("display", run.Tags[ReportTelemetry.ModeTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, run.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
    }

    [Fact]
    public void Serialize_RecordsTheExportByFormatAndSource()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var descriptor = ReportExportFormatDescriptor.Create<SampleFormat>();
        var document = new ReportDocument()
            .Add(ReportSection.ForTable("Rows", [new ReportColumn("A")], [new ReportRow(), new ReportRow(), new ReportRow()]));

        // Act
        var content = ReportTelemetry.Serialize(new SampleFormat(), descriptor, document, ReportRunSources.Subscription);

        // Assert
        Assert.Equal(4, content.Length);
        Assert.Equal(4d, Assert.Single(capture.Find("crestapps.report.export.size", CrestAppsOrchardCoreTelemetry.FormatTag, SampleFormat.Name)).Value);
        Assert.Equal(3d, Assert.Single(capture.Find("crestapps.report.export.rows", CrestAppsOrchardCoreTelemetry.FormatTag, SampleFormat.Name)).Value);

        var duration = Assert.Single(capture.Find("crestapps.report.export.duration", CrestAppsOrchardCoreTelemetry.FormatTag, SampleFormat.Name));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        var export = Assert.Single(capture.Find("crestapps.report.exports", CrestAppsOrchardCoreTelemetry.FormatTag, SampleFormat.Name));
        Assert.Equal(ReportRunSources.Subscription, export.Tags[ReportTelemetry.RunSourceTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, export.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Contains(capture.Spans, span => span.OperationName == "report.export");
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void RecordExport_WithAnUnknownSourceAndFormat_TagsBoundedValues()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act: a source and a format typed in an address never become tag values.
        ReportTelemetry.RecordExport("https://example.test/free-text", null, ReportTelemetry.OutcomeNotFound);

        // Assert
        var export = Assert.Single(capture.Find("crestapps.report.exports", CrestAppsOrchardCoreTelemetry.OutcomeTag, ReportTelemetry.OutcomeNotFound));
        Assert.Equal(ReportRunSources.Other, export.Tags[ReportTelemetry.RunSourceTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.Other, export.Tags[CrestAppsOrchardCoreTelemetry.FormatTag]);
    }

    [Fact]
    public void RecordCodedIndexRows_AtTheThreshold_RecordsTheRowsAndLogsAWarning()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var logger = new ListLogger();
        var name = "index-" + Guid.NewGuid().ToString("n");

        // Act
        ReportTelemetry.RecordCodedIndexRows(name, 10, logger);
        ReportTelemetry.RecordCodedIndexRows(name, ReportTelemetry.CodedIndexRowsWarningThreshold, logger);

        // Assert
        Assert.Equal(2, capture.Find("crestapps.report.coded.index_rows", ReportTelemetry.ReportNameTag, name).Count);

        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Equal(CrestAppsEventIds.Reports + 1, warning.EventId.Id);
    }

    [Fact]
    public void ModuleRecorders_EmitTheirInstrumentsWithBoundedTagsOnly()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        ReportTelemetry.RecordViewRefresh("refreshed", ReportTelemetry.TriggerSchedule);
        ReportTelemetry.RecordViewRead("stale_snapshot");
        ReportTelemetry.RecordViewRejected("loop");
        ReportTelemetry.RecordSubscriptionSend(CrestAppsOrchardCoreTelemetry.OutcomeSkipped, "no_recipients", ReportTelemetry.TriggerManual);
        ReportTelemetry.RecordAggregatePushdown(ReportsConstants.ContentsDataSource, "joins");
        ReportTelemetry.RecordSharedReadMiss(ReportsConstants.ContentsDataSource, "conditions");
        ReportTelemetry.Count(ReportTelemetry.DesignConflicts, (CrestAppsOrchardCoreTelemetry.ReasonTag, "busy"));

        // Assert
        var refresh = Assert.Single(capture.Find("crestapps.report.view.refreshes", CrestAppsOrchardCoreTelemetry.OutcomeTag, "refreshed"));
        Assert.Equal(ReportTelemetry.TriggerSchedule, refresh.Tags[CrestAppsOrchardCoreTelemetry.TriggerTag]);
        Assert.Single(capture.Find("crestapps.report.view.reads", ReportTelemetry.ViewReadModeTag, "stale_snapshot"));
        Assert.Single(capture.Find("crestapps.report.view.rejected", CrestAppsOrchardCoreTelemetry.ReasonTag, "loop"));

        var send = Assert.Single(capture.Find("crestapps.report.subscription.sends", CrestAppsOrchardCoreTelemetry.ReasonTag, "no_recipients"));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeSkipped, send.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        var pushdown = Assert.Single(capture.Find("crestapps.report.aggregate.pushdown", CrestAppsOrchardCoreTelemetry.ReasonTag, "joins"));
        Assert.Equal("fallback", pushdown.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Single(capture.Find("crestapps.report.reads.shared.miss", CrestAppsOrchardCoreTelemetry.ReasonTag, "conditions"));
        Assert.Single(capture.Find("crestapps.report.design.conflicts", CrestAppsOrchardCoreTelemetry.ReasonTag, "busy"));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Theory]
    [InlineData("Admin", "Admin")]
    [InlineData("widget", "Widget")]
    [InlineData("ViewRefresh", "ViewRefresh")]
    [InlineData(null, "Other")]
    [InlineData("report-123", "Other")]
    public void Normalize_MapsASourceToTheBoundedSet(string source, string expected)
    {
        Assert.Equal(expected, ReportRunSources.Normalize(source));
    }

    private static ReportDescriptor Descriptor(string name)
    {
        return new ReportDescriptor(
            name,
            LocalizationSource.Create("Sample"),
            null,
            "Tests",
            new Permission("ViewSampleReport"),
            typeof(FixedReport),
            _ => null);
    }

    private sealed class FixedReport : IReportRunner
    {
        private readonly ReportDocument _document;

        public FixedReport(ReportDocument document)
        {
            _document = document;
        }

        public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
        {
            return _document is null
                ? Task.FromException<ReportDocument>(new InvalidOperationException("The report failed."))
                : Task.FromResult(_document);
        }
    }

    private sealed class SampleFormat : IReportExportFormat
    {
        public static string Name => "telemetry-sample";

        public static LocalizationSource DisplayName { get; } = LocalizationSource.Create("Sample");

        public static string Extension => ".bin";

        public static string ContentType => "application/octet-stream";

        public byte[] Serialize(ReportDocument document)
        {
            return [1, 2, 3, 4];
        }
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, EventId EventId)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Entries.Add((logLevel, eventId));
        }
    }
}
