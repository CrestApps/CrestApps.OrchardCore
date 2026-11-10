using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContentTransfer.Telemetry;

/// <summary>
/// The OpenTelemetry contract of Content Transfer: the <c>CrestApps.OrchardCore.ContentTransfer</c> meter and activity
/// source, and the <c>crestapps.contenttransfer.*</c> instruments of imports, exports, uploads, row filters, content
/// import handlers and the file store. The names are a public integration surface.
/// </summary>
/// <remarks>
/// No instrument carries an entry id, a file name, a row index, a user or a batch id; those go on spans and in logs
/// only. Reasons and results are bounded codes, never the localized text shown to users. Nothing is recorded while no
/// listener is attached.
/// </remarks>
public static class ContentTransferDiagnostics
{
    /// <summary>
    /// The name of the meter and the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".ContentTransfer";

    /// <summary>
    /// The tag that records the technical name of the content type imported or exported.
    /// </summary>
    public const string ContentTypeTag = "content_type";

    /// <summary>
    /// The tag that records how an export ran: <see cref="ModeInline"/> or <see cref="ModeQueued"/>.
    /// </summary>
    public const string ModeTag = "mode";

    /// <summary>
    /// The tag that records the phase of the work, such as <see cref="PhasePrepare"/> or <see cref="PhaseImport"/>.
    /// </summary>
    public const string PhaseTag = "phase";

    /// <summary>
    /// The tag that records the content import handler, by its type name.
    /// </summary>
    public const string HandlerTag = "handler";

    /// <summary>
    /// The tag that records the kind of job started after a request: <c>import</c>, <c>export</c> or <c>delete</c>.
    /// </summary>
    public const string JobTag = "job";

    /// <summary>
    /// The tag that records the storage behind the Content Transfer file store: <c>file_system</c>, <c>azure_blob</c>
    /// or <c>other</c>.
    /// </summary>
    public const string BackendTag = "backend";

    /// <summary>
    /// The tag that records which value an export handler had to look up row by row because the page was not prepared.
    /// </summary>
    public const string LookupTag = "lookup";

    /// <summary>
    /// The span tag that records the id of the import or export entry. Never a metric tag.
    /// </summary>
    public const string EntryIdSpanTag = "crestapps.contenttransfer.entry.id";

    /// <summary>
    /// The span tag that records the first row of a batch. Never a metric tag.
    /// </summary>
    public const string FirstRowSpanTag = "crestapps.contenttransfer.row.first";

    /// <summary>
    /// The span tag that records the number of rows of a batch or a page.
    /// </summary>
    public const string RowsSpanTag = "crestapps.contenttransfer.rows";

    /// <summary>
    /// The import finished every row of its file.
    /// </summary>
    public const string OutcomeCompleted = "completed";

    /// <summary>
    /// The import run reached its time limit, saved its progress and continues under a fresh lock.
    /// </summary>
    public const string OutcomeContinued = "continued";

    /// <summary>
    /// The import run stopped because the import was paused or deleted, or the host is stopping.
    /// </summary>
    public const string OutcomeStopped = "stopped";

    /// <summary>
    /// The import run failed and the import is tried again from its last saved row.
    /// </summary>
    public const string OutcomeRetry = "retry";

    /// <summary>
    /// The watchdog gave up on the import run.
    /// </summary>
    public const string OutcomeAbandoned = "abandoned";

    /// <summary>
    /// The import failed and was marked as failed.
    /// </summary>
    public const string OutcomeFailed = "failed";

    /// <summary>
    /// The upload was accepted and its import queued.
    /// </summary>
    public const string OutcomeAccepted = "accepted";

    /// <summary>
    /// The upload was rejected.
    /// </summary>
    public const string OutcomeRejected = "rejected";

    /// <summary>
    /// A row was imported.
    /// </summary>
    public const string ResultImported = "imported";

    /// <summary>
    /// A row failed to import.
    /// </summary>
    public const string ResultError = "error";

    /// <summary>
    /// A row filter skipped a row.
    /// </summary>
    public const string ResultSkippedByFilter = "skipped_by_filter";

    /// <summary>
    /// A row was skipped because importing it stopped responding run after run.
    /// </summary>
    public const string ResultSkippedStalled = "skipped_stalled";

    /// <summary>
    /// A row had no value in any cell.
    /// </summary>
    public const string ResultEmpty = "empty";

    /// <summary>
    /// An exported row was written to the file.
    /// </summary>
    public const string ResultWritten = "written";

    /// <summary>
    /// A handler excluded an exported row from the file.
    /// </summary>
    public const string ResultExcluded = "excluded";

    /// <summary>
    /// A row failed the content item's validation.
    /// </summary>
    public const string ReasonValidation = "validation";

    /// <summary>
    /// Importing a row threw an exception.
    /// </summary>
    public const string ReasonHandlerException = "handler_exception";

    /// <summary>
    /// A row stopped responding past the watchdog's row timeout.
    /// </summary>
    public const string ReasonRowTimeout = "row_timeout";

    /// <summary>
    /// A run outlived the watchdog's run timeout.
    /// </summary>
    public const string ReasonRunTimeout = "run_timeout";

    /// <summary>
    /// The host stopped while the run was at work.
    /// </summary>
    public const string ReasonHostStopping = "host_stopping";

    /// <summary>
    /// The export ran in the request that asked for it.
    /// </summary>
    public const string ModeInline = "inline";

    /// <summary>
    /// The export was queued and ran in the background.
    /// </summary>
    public const string ModeQueued = "queued";

    /// <summary>
    /// A row filter prepares for a batch of rows.
    /// </summary>
    public const string PhasePrepare = "prepare";

    /// <summary>
    /// A row filter decides on each row of a batch.
    /// </summary>
    public const string PhaseEvaluate = "evaluate";

    /// <summary>
    /// A content import handler maps a row onto a content item.
    /// </summary>
    public const string PhaseImport = "import";

    /// <summary>
    /// A content import handler maps a content item onto a row.
    /// </summary>
    public const string PhaseExport = "export";

    /// <summary>
    /// An export batch handler prepares a page of content items.
    /// </summary>
    public const string PhasePrepareExport = "prepare_export";

    private const int MaxCodeLength = 40;

    private static readonly ConcurrentDictionary<object, Func<TimeSpan?>> _rowsInFlight = new();

    /// <summary>
    /// Gets the meter of Content Transfer.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("ContentTransfer");

    /// <summary>
    /// Gets the activity source of Content Transfer.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("ContentTransfer");

    /// <summary>
    /// Gets the number of import runs, tagged with the <c>outcome</c> (<see cref="OutcomeCompleted"/>,
    /// <see cref="OutcomeContinued"/>, <see cref="OutcomeStopped"/>, <see cref="OutcomeRetry"/>,
    /// <see cref="OutcomeAbandoned"/>, <see cref="OutcomeFailed"/>) and the <see cref="ContentTypeTag"/>.
    /// </summary>
    public static Counter<long> ImportRuns { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.runs",
        "{run}",
        "The number of import runs, by how they ended.");

    /// <summary>
    /// Gets the number of import runs at work right now on this node.
    /// </summary>
    public static UpDownCounter<long> ImportActive { get; } = Meter.CreateUpDownCounter<long>(
        "crestapps.contenttransfer.import.active",
        "{run}",
        "The number of import runs at work.");

    /// <summary>
    /// Gets the number of times an import was found locked by another run.
    /// </summary>
    public static Counter<long> ImportLockContended { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.lock_contended",
        "{attempt}",
        "The number of times an import was found locked by another run.");

    /// <summary>
    /// Gets the number of times a run tried an import again after a failed run, tagged with the <c>reason</c>.
    /// </summary>
    public static Counter<long> ImportRetries { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.retries",
        "{retry}",
        "The number of times an import was tried again after a failed run.");

    /// <summary>
    /// Gets the duration of import runs, tagged with the <c>outcome</c>, the <see cref="ContentTypeTag"/> and the <c>format</c>.
    /// </summary>
    public static Histogram<double> ImportDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.import.duration",
        "The duration of one import run under its lock.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the size of imported files, recorded when their import starts, tagged with the <c>format</c>.
    /// </summary>
    public static Histogram<long> ImportFileSize { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.contenttransfer.import.file.size",
        "By",
        "The size of the files imported.",
        CrestAppsOrchardCoreTelemetry.SizeBucketBoundaries);

    /// <summary>
    /// Gets the number of rows of imported files, recorded when their import starts, tagged with the <c>format</c>.
    /// </summary>
    public static Histogram<long> ImportRowsTotal { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.contenttransfer.import.rows.total",
        "{row}",
        "The number of rows in the files imported.");

    /// <summary>
    /// Gets the number of imports that were marked as failed, tagged with the <c>reason</c>.
    /// </summary>
    public static Counter<long> ImportEntriesFailed { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.entries.failed",
        "{entry}",
        "The number of imports marked as failed, by reason.");

    /// <summary>
    /// Gets the number of failed import runs whose failure was recorded on the import, tagged with the <c>reason</c>:
    /// <c>transient</c>, <c>resumable</c>, <c>final</c> or <c>unrecorded</c>.
    /// </summary>
    public static Counter<long> ImportFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.failures",
        "{run}",
        "The number of import runs that failed, by how the failure was handled.");

    /// <summary>
    /// Gets the number of rows an abandoned run was stuck on, tagged with the <c>result</c>: <c>recorded</c> or <c>unrecorded</c>.
    /// </summary>
    public static Counter<long> ImportStalledRows { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.stalled_rows",
        "{row}",
        "The number of rows an abandoned import run was stuck on.");

    /// <summary>
    /// Gets the duration of import batches, from the status check to the saved progress, tagged with the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> ImportBatchDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.import.batch.duration",
        "The duration of one import batch.");

    /// <summary>
    /// Gets the number of rows of import batches.
    /// </summary>
    public static Histogram<long> ImportBatchSize { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.contenttransfer.import.batch.size",
        "{row}",
        "The number of rows in one import batch.");

    /// <summary>
    /// Gets the number of rows read by imports, counted once their progress is saved, tagged with the <c>result</c>:
    /// <see cref="ResultImported"/>, <see cref="ResultError"/>, <see cref="ResultSkippedByFilter"/>,
    /// <see cref="ResultSkippedStalled"/> or <see cref="ResultEmpty"/>.
    /// </summary>
    public static Counter<long> ImportRows { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.rows",
        "{row}",
        "The number of rows read by imports, by what became of them.");

    /// <summary>
    /// Gets the number of rows row filters skipped, counted once their progress is saved, tagged with the <c>reason</c>
    /// code the filter set in <see cref="ContentImportRowFilterContext.SkipReasonCode"/>, or <c>other</c>.
    /// </summary>
    public static Counter<long> ImportRowsSkipped { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.rows.skipped",
        "{row}",
        "The number of rows row filters skipped, by reason.");

    /// <summary>
    /// Gets the number of rows that failed to import, counted once their progress is saved, tagged with the
    /// <c>reason</c>: <see cref="ReasonValidation"/> or <see cref="ReasonHandlerException"/>.
    /// </summary>
    public static Counter<long> ImportRowErrors { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.row.errors",
        "{row}",
        "The number of rows that failed to import, by reason.");

    /// <summary>
    /// Gets the number of import runs the watchdog gave up on, tagged with the <c>reason</c>:
    /// <see cref="ReasonRowTimeout"/>, <see cref="ReasonRunTimeout"/> or <see cref="ReasonHostStopping"/>.
    /// </summary>
    public static Counter<long> ImportAbandoned { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.abandoned",
        "{run}",
        "The number of import runs the watchdog gave up on, by reason.");

    /// <summary>
    /// Gets the number of rows that ran past the watchdog's slow-row threshold.
    /// </summary>
    public static Counter<long> ImportSlowRows { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.slow_rows",
        "{row}",
        "The number of import rows that ran past the slow-row threshold.");

    /// <summary>
    /// Gets how long the row an abandoned run was on had been running, tagged with the <c>reason</c>.
    /// </summary>
    public static Histogram<double> ImportStalledRowElapsed { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.import.stalled_row.elapsed",
        "How long the row an abandoned import run was on had been running.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets how long the oldest import row in flight on this node has been running. No value is reported while no
    /// row is in flight.
    /// </summary>
    public static ObservableGauge<double> ImportRowInFlightAge { get; } = Meter.CreateObservableGauge(
        "crestapps.contenttransfer.import.row_in_flight.age",
        ObserveRowInFlightAge,
        "s",
        "How long the oldest import row in flight has been running.");

    /// <summary>
    /// Gets the duration of the row filters on a batch of rows, tagged with the <see cref="PhaseTag"/>:
    /// <see cref="PhasePrepare"/> or <see cref="PhaseEvaluate"/>.
    /// </summary>
    public static Histogram<double> ImportFilterDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.import.filter.duration",
        "The duration of the row filters on one batch of rows.");

    /// <summary>
    /// Gets the number of upload requests of import files, tagged with the <c>outcome</c> (<see cref="OutcomeAccepted"/>
    /// or <see cref="OutcomeRejected"/>) and, when rejected, the <c>reason</c>.
    /// </summary>
    public static Counter<long> ImportUploads { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.import.uploads",
        "{upload}",
        "The number of import file uploads, by outcome.");

    /// <summary>
    /// Gets the size of the accepted import files, tagged with the <c>format</c>.
    /// </summary>
    public static Histogram<long> UploadSize { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.contenttransfer.upload.size",
        "By",
        "The size of the import files uploaded.",
        CrestAppsOrchardCoreTelemetry.SizeBucketBoundaries);

    /// <summary>
    /// Gets the number of chunks of chunked uploads, tagged with the <c>result</c>: <c>stored</c>, <c>completed</c>
    /// or <c>rejected</c>.
    /// </summary>
    public static Counter<long> UploadChunks { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.upload.chunks",
        "{chunk}",
        "The number of chunks of chunked import uploads, by result.");

    /// <summary>
    /// Gets the size of the chunks of chunked uploads.
    /// </summary>
    public static Histogram<long> UploadChunkSize { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.contenttransfer.upload.chunk.size",
        "By",
        "The size of the chunks of chunked import uploads.",
        CrestAppsOrchardCoreTelemetry.SizeBucketBoundaries);

    /// <summary>
    /// Gets the duration of queued exports, tagged with the <c>format</c>, the <c>outcome</c> and the <see cref="ContentTypeTag"/>.
    /// </summary>
    public static Histogram<double> ExportDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.export.duration",
        "The duration of one queued export.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the duration of exports that ran in the request that asked for them, tagged with the <c>format</c>, the
    /// <c>outcome</c> and the <see cref="ContentTypeTag"/>.
    /// </summary>
    public static Histogram<double> ExportInlineDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.export.inline.duration",
        "The duration of one export run in the request that asked for it.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the number of exported content items, tagged with the <see cref="ModeTag"/> and the <c>result</c>:
    /// <see cref="ResultWritten"/> or <see cref="ResultExcluded"/>.
    /// </summary>
    public static Counter<long> ExportRows { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.export.rows",
        "{row}",
        "The number of content items exports read, by whether they were written.");

    /// <summary>
    /// Gets the duration of export pages, from the query to the last written row, tagged with the <see cref="ModeTag"/>.
    /// </summary>
    public static Histogram<double> ExportPageDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.export.page.duration",
        "The duration of one page of an export.");

    /// <summary>
    /// Gets the number of export pages, tagged with the <see cref="ModeTag"/>.
    /// </summary>
    public static Counter<long> ExportPages { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.export.pages",
        "{page}",
        "The number of pages exports read.");

    /// <summary>
    /// Gets the size of the exported files, tagged with the <c>format</c> and the <see cref="ModeTag"/>.
    /// </summary>
    public static Histogram<long> ExportFileSize { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.contenttransfer.export.file.size",
        "By",
        "The size of the exported files.",
        CrestAppsOrchardCoreTelemetry.SizeBucketBoundaries);

    /// <summary>
    /// Gets the number of exports that failed, tagged with the <c>reason</c> and the <see cref="ModeTag"/>.
    /// </summary>
    public static Counter<long> ExportFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.export.failures",
        "{export}",
        "The number of exports that failed, by reason.");

    /// <summary>
    /// Gets the number of export requests, tagged with the <see cref="ModeTag"/> and the <c>format</c>.
    /// </summary>
    public static Counter<long> ExportRequests { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.export.requests",
        "{request}",
        "The number of export requests, by whether they ran in the request or were queued.");

    /// <summary>
    /// Gets the number of values an export handler looked up row by row because the page was not prepared first,
    /// tagged with the <see cref="LookupTag"/>.
    /// </summary>
    public static Counter<long> ExportLookupFallbacks { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.export.lookup_fallbacks",
        "{lookup}",
        "The number of values an export handler looked up row by row instead of from the prepared page.");

    /// <summary>
    /// Gets the number of jobs started after the request that queued them, tagged with the <see cref="JobTag"/>.
    /// </summary>
    public static Counter<long> JobsTriggered { get; } = Meter.CreateCounter<long>(
        "crestapps.contenttransfer.jobs.triggered",
        "{job}",
        "The number of import, export and delete jobs queued to run after the request.");

    /// <summary>
    /// Gets the duration of the Content Transfer file store operations, tagged with the <c>operation</c>, the
    /// <see cref="BackendTag"/> and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> StoreDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.store.duration",
        "The duration of the Content Transfer file store operations.");

    /// <summary>
    /// Gets the duration of the content import handlers, tagged with the <see cref="HandlerTag"/>, the
    /// <see cref="PhaseTag"/> (<see cref="PhaseImport"/>, <see cref="PhaseExport"/> or <see cref="PhasePrepareExport"/>)
    /// and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> HandlerDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.handler.duration",
        "The duration of one content import handler on one row or page.");

    /// <summary>
    /// Gets the duration of the file readers and writers, tagged with the <c>format</c> and the <c>operation</c>, such
    /// as <c>count_rows</c> or <c>write_flush</c>.
    /// </summary>
    public static Histogram<double> FileOperationDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contenttransfer.file.operation.duration",
        "The duration of the work a file reader or writer does at once, such as counting the rows of a file.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the bounded <c>format</c> tag of a file extension or a file name.
    /// </summary>
    /// <param name="extensionOrFileName">The extension, with or without its dot, or a file name.</param>
    /// <returns>The extension in lower case, such as <c>csv</c> or <c>xlsx</c>, or <c>other</c> when it is not a
    /// format Content Transfer reads or writes.</returns>
    public static string GetFormat(string extensionOrFileName)
    {
        if (string.IsNullOrEmpty(extensionOrFileName))
        {
            return CrestAppsOrchardCoreTelemetry.Unknown;
        }

        var extension = Path.GetExtension(extensionOrFileName);

        if (string.IsNullOrEmpty(extension))
        {
            extension = extensionOrFileName;
        }

        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "csv" => "csv",
            "tsv" => "tsv",
            "txt" => "txt",
            "xlsx" => "xlsx",
            "xls" => "xls",
            "json" => "json",
            "xml" => "xml",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Gets a bounded tag value from a code set by an extension, such as a row filter's skip reason.
    /// </summary>
    /// <param name="code">The code, in snake_case.</param>
    /// <returns>The code when it is made of lower-case letters, digits and underscores only and is short; otherwise <c>other</c>.</returns>
    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength)
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        foreach (var character in code)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return CrestAppsOrchardCoreTelemetry.Other;
            }
        }

        return code;
    }

    /// <summary>
    /// Gets the bounded <see cref="BackendTag"/> value of the file store behind the Content Transfer file store.
    /// </summary>
    /// <param name="fileStore">The file store, or <see langword="null"/>.</param>
    /// <returns><c>file_system</c>, <c>azure_blob</c>, <c>aws_s3</c> or <c>other</c>.</returns>
    public static string GetBackend(object fileStore)
    {
        return fileStore?.GetType().Name switch
        {
            "FileSystemStore" => "file_system",
            "BlobFileStore" => "azure_blob",
            "AwsFileStore" => "aws_s3",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Stores the <c>traceparent</c> of the current span on an entry, so its background import or export links back
    /// to the request that queued it.
    /// </summary>
    /// <param name="entry">The entry about to be saved.</param>
    public static void SetTraceParent(ContentTransferEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var traceParent = TraceContextPropagation.Capture();

        if (traceParent is null || entry.Properties is null)
        {
            return;
        }

        entry.Properties[TraceContextPropagation.TraceParentKey] = traceParent;
    }

    /// <summary>
    /// Gets the <c>traceparent</c> stored on an entry with <see cref="SetTraceParent(ContentTransferEntry)"/>.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The <c>traceparent</c>, or <see langword="null"/> when none is stored.</returns>
    public static string GetTraceParent(ContentTransferEntry entry)
    {
        if (entry?.Properties is null
            || !entry.Properties.TryGetPropertyValue(TraceContextPropagation.TraceParentKey, out var node)
            || node is not JsonValue value
            || !value.TryGetValue<string>(out var traceParent))
        {
            return null;
        }

        return traceParent;
    }

    /// <summary>
    /// Adds to a counter with one tag, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="key">The tag key.</param>
    /// <param name="tagValue">The bounded tag value.</param>
    public static void Add(Counter<long> counter, long value, string key, object tagValue)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (value <= 0 || !counter.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { key, tagValue },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    /// <summary>
    /// Adds to a counter with the given tags, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="tags">The bounded tags.</param>
    public static void Add(Counter<long> counter, long value, TagList tags = default)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (value <= 0 || !counter.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    /// <summary>
    /// Adds to an up-down counter, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="counter">The up-down counter.</param>
    /// <param name="value">The value to add, negative to subtract.</param>
    public static void Add(UpDownCounter<long> counter, long value)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (!counter.Enabled)
        {
            return;
        }

        var tags = new TagList();
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    /// <summary>
    /// Records a value on a histogram with the given tags, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="histogram">The histogram.</param>
    /// <param name="value">The value to record.</param>
    /// <param name="tags">The bounded tags.</param>
    public static void Record(Histogram<long> histogram, long value, TagList tags = default)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        if (!histogram.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        histogram.Record(value, tags);
    }

    /// <summary>
    /// Records a value on a histogram with the given tags, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="histogram">The histogram.</param>
    /// <param name="value">The value to record.</param>
    /// <param name="tags">The bounded tags.</param>
    public static void Record(Histogram<double> histogram, double value, TagList tags = default)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        if (!histogram.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        histogram.Record(value, tags);
    }

    /// <summary>
    /// Records that an export handler looked a value up row by row because the page was not prepared first.
    /// </summary>
    /// <param name="lookup">What was looked up, from a bounded set such as <c>completed_by</c> or <c>disposition</c>.</param>
    public static void RecordExportLookupFallback(string lookup)
    {
        Add(ExportLookupFallbacks, 1, LookupTag, NormalizeCode(lookup));
    }

    /// <summary>
    /// Runs one content import handler and records its duration on <see cref="HandlerDuration"/>.
    /// </summary>
    /// <param name="handler">The handler.</param>
    /// <param name="phase">The phase: <see cref="PhaseImport"/>, <see cref="PhaseExport"/> or <see cref="PhasePrepareExport"/>.</param>
    /// <param name="work">The work of the handler.</param>
    /// <returns>A task that completes when the handler completes. Exceptions are recorded and rethrown.</returns>
    public static Task MeasureHandlerAsync(object handler, string phase, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (!HandlerDuration.Enabled || handler is null)
        {
            return work();
        }

        return MeasureHandlerCoreAsync(handler.GetType().Name, phase, work);
    }

    /// <summary>
    /// Watches how long an import row has been in flight, for <see cref="ImportRowInFlightAge"/>.
    /// </summary>
    /// <param name="rowAge">Returns how long the current row has been running, or <see langword="null"/> between rows.
    /// It is called from the metrics collection thread.</param>
    /// <returns>A registration that stops watching when disposed.</returns>
    public static IDisposable TrackRowInFlight(Func<TimeSpan?> rowAge)
    {
        ArgumentNullException.ThrowIfNull(rowAge);

        var key = new object();
        _rowsInFlight[key] = rowAge;

        return new RowInFlightRegistration(key);
    }

    private static async Task MeasureHandlerCoreAsync(string handler, string phase, Func<Task> work)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var outcome = CrestAppsOrchardCoreTelemetry.OutcomeError;

        try
        {
            await work();
            outcome = CrestAppsOrchardCoreTelemetry.OutcomeOk;
        }
        catch (Exception ex)
        {
            outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(ex);

            throw;
        }
        finally
        {
            Record(HandlerDuration, Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, new TagList
            {
                { HandlerTag, handler },
                { PhaseTag, phase },
                { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
            });
        }
    }

    private static IEnumerable<Measurement<double>> ObserveRowInFlightAge()
    {
        double? oldest = null;

        foreach (var rowAge in _rowsInFlight.Values)
        {
            if (rowAge() is { } age && (oldest is null || age.TotalSeconds > oldest))
            {
                oldest = age.TotalSeconds;
            }
        }

        return oldest is { } seconds
            ? [new Measurement<double>(seconds)]
            : [];
    }

    private sealed class RowInFlightRegistration : IDisposable
    {
        private readonly object _key;

        public RowInFlightRegistration(object key)
        {
            _key = key;
        }

        public void Dispose()
        {
            _rowsInFlight.TryRemove(_key, out _);
        }
    }
}
