using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Reports.Designer;

public static partial class ReportTelemetry
{
    /// <summary>
    /// The tag that holds the technical name of a coded report from the report registry, such as <c>DialerCallDetail</c>.
    /// </summary>
    public const string ReportNameTag = "crestapps.report.name";

    /// <summary>
    /// The tag that holds how a coded report runs: <c>display</c> or <c>export</c>.
    /// </summary>
    public const string ModeTag = "crestapps.report.mode";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> value of an export of a format that is not registered.
    /// </summary>
    public const string OutcomeNotFound = "not_found";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> value of an export the person may not make.
    /// </summary>
    public const string OutcomeForbidden = "forbidden";

    /// <summary>
    /// The number of index rows a coded report reads at once above which <see cref="RecordCodedIndexRows(string, long, ILogger)"/>
    /// logs a warning: the report loads that many records into memory, so it needs a narrower filter or a paged read.
    /// </summary>
    public const long CodedIndexRowsWarningThreshold = 50_000;

    /// <summary>
    /// Gets the histogram of the duration of coded report runs, tagged with the <see cref="ReportNameTag"/>, the
    /// <see cref="ModeTag"/> and the outcome.
    /// </summary>
    public static Histogram<double> CodedDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.coded.duration",
        "The duration of coded report runs.");

    /// <summary>
    /// Gets the histogram of the index rows a coded report read at once, tagged with the <see cref="ReportNameTag"/>.
    /// </summary>
    public static Histogram<long> CodedIndexRows { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.coded.index_rows",
        "{row}",
        "The index rows a coded report read at once.",
        [100, 1_000, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, 1_000_000]);

    /// <summary>
    /// Gets the histogram of the time a report export took to write its file, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.FormatTag"/> and the outcome.
    /// </summary>
    public static Histogram<double> ExportDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.export.duration",
        "The time report exports took to write their file.");

    /// <summary>
    /// Gets the histogram of the size of the files report exports wrote, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.FormatTag"/>.
    /// </summary>
    public static Histogram<long> ExportSize { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.export.size",
        "By",
        "The size of the files report exports wrote.",
        CrestAppsOrchardCoreTelemetry.SizeBucketBoundaries);

    /// <summary>
    /// Gets the histogram of the table rows of the files report exports wrote, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.FormatTag"/>.
    /// </summary>
    public static Histogram<long> ExportRows { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.export.rows",
        "{row}",
        "The table rows of the files report exports wrote.");

    /// <summary>
    /// Gets the counter of report export requests, tagged with the source, the <see cref="CrestAppsOrchardCoreTelemetry.FormatTag"/>
    /// and the outcome: <c>ok</c>, <c>error</c>, <see cref="OutcomeNotFound"/> or <see cref="OutcomeForbidden"/>.
    /// </summary>
    public static Counter<long> Exports { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.exports",
        "{export}",
        "The report export requests, by how they ended.");

    /// <summary>
    /// Runs a coded report and measures the run: a <c>report.coded.run</c> span and <see cref="CodedDuration"/>, tagged
    /// with the outcome.
    /// </summary>
    /// <param name="runner">The report, from <see cref="Services.IReportManager.GetReport(string)"/>.</param>
    /// <param name="descriptor">The descriptor of the report in the registry, whose name tags the metrics.</param>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The report document. Exceptions are recorded and rethrown.</returns>
    public static async Task<ReportDocument> RunCodedReportAsync(
        IReportRunner runner,
        ReportDescriptor descriptor,
        ReportContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);

        var tags = new TagList
        {
            { ReportNameTag, descriptor.Name },
            { ModeTag, context.Mode == ReportRunMode.Export ? "export" : "display" },
        };

        using var telemetry = TelemetryScope.Start(ActivitySource, "report.coded.run", CodedDuration, tags);

        try
        {
            var document = await runner.RunAsync(context, cancellationToken);

            telemetry.Succeed();

            return document;
        }
        catch (Exception exception)
        {
            telemetry.Fail(exception);

            throw;
        }
    }

    /// <summary>
    /// Records how many index rows a coded report read at once, and logs a warning when they reach
    /// <see cref="CodedIndexRowsWarningThreshold"/>. Call it from a report that lists the records of an index without a
    /// page size, with the number of records it got back.
    /// </summary>
    /// <param name="reportName">The technical name of the report in the registry, never free text.</param>
    /// <param name="rows">The number of index rows read.</param>
    /// <param name="logger">The logger of the report, or <see langword="null"/> to record the metric only.</param>
    public static void RecordCodedIndexRows(string reportName, long rows, ILogger logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(reportName);

        Activity.Current?.SetTag("crestapps.report.index_rows", rows);

        if (CodedIndexRows.Enabled)
        {
            var tags = new TagList
            {
                { ReportNameTag, reportName },
            };

            CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
            CodedIndexRows.Record(rows, tags);
        }

        if (rows >= CodedIndexRowsWarningThreshold && logger is not null)
        {
            ReportsLog.CodedIndexRowsAboveThreshold(logger, reportName, rows, CodedIndexRowsWarningThreshold);
        }
    }

    /// <summary>
    /// Writes a report document in an export format and measures it: a <c>report.export</c> span,
    /// <see cref="ExportDuration"/>, <see cref="ExportSize"/>, <see cref="ExportRows"/> and <see cref="Exports"/>.
    /// </summary>
    /// <param name="format">The export format, from <see cref="Services.IReportExportManager.GetFormat(string)"/>.</param>
    /// <param name="descriptor">The descriptor of the format in the registry, whose name tags the metrics.</param>
    /// <param name="document">The report document.</param>
    /// <param name="source">What the export is for, one of the values of <see cref="ReportRunSources"/>.</param>
    /// <returns>The content of the file. Exceptions are recorded and rethrown.</returns>
    public static byte[] Serialize(
        IReportExportFormat format,
        ReportExportFormatDescriptor descriptor,
        ReportDocument document,
        string source)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(document);

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.FormatTag, descriptor.Name },
        };

        using var telemetry = TelemetryScope.Start(ActivitySource, "report.export", ExportDuration, tags);

        telemetry.SetSpanTag(RunSourceTag, ReportRunSources.Normalize(source));

        try
        {
            var content = format.Serialize(document);

            telemetry.Succeed();

            if (ExportSize.Enabled || ExportRows.Enabled || telemetry.Activity is not null)
            {
                var rows = document.Sections?.Where(section => section?.Rows is not null).Sum(section => (long)section.Rows.Count) ?? 0;
                var metricTags = tags;

                CrestAppsOrchardCoreTelemetry.AddTenantTag(ref metricTags);
                ExportSize.Record(content?.LongLength ?? 0, metricTags);
                ExportRows.Record(rows, metricTags);
                telemetry.SetSpanTag("crestapps.report.export.size", content?.LongLength ?? 0);
                telemetry.SetSpanTag("crestapps.report.export.rows", rows);
            }

            return content;
        }
        catch (Exception exception)
        {
            telemetry.Fail(exception);

            throw;
        }
        finally
        {
            RecordExport(source, descriptor.Name, telemetry.Outcome);
        }
    }

    /// <summary>
    /// Records a report export request, such as one refused because its format is unknown or the person may not export.
    /// </summary>
    /// <param name="source">What the export is for, one of the values of <see cref="ReportRunSources"/>.</param>
    /// <param name="format">The name of a registered format, or <see langword="null"/> for an unknown one.</param>
    /// <param name="outcome"><c>ok</c>, <c>error</c>, <see cref="OutcomeNotFound"/> or <see cref="OutcomeForbidden"/>.</param>
    public static void RecordExport(string source, string format, string outcome)
    {
        if (!Exports.Enabled)
        {
            return;
        }

        var tags = Tags(source);

        tags.Add(CrestAppsOrchardCoreTelemetry.FormatTag, string.IsNullOrEmpty(format) ? CrestAppsOrchardCoreTelemetry.Other : format);
        tags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome ?? CrestAppsOrchardCoreTelemetry.Unknown);
        Exports.Add(1, tags);
    }
}
