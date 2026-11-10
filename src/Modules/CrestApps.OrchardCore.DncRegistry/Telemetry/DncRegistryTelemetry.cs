using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.DncRegistry.Telemetry;

/// <summary>
/// The OpenTelemetry contract of the do-not-call registry module: the <c>CrestApps.OrchardCore.DncRegistry</c> meter
/// and activity source, and the <c>crestapps.dnc.*</c> instruments that measure how numbers are screened against a
/// registry and how local do-not-call lists are imported. Instrument names and tags are a public contract.
/// </summary>
/// <remarks>
/// No instrument ever carries a phone number, a list id or a list name. Screening is tagged with the bounded
/// <see cref="RegistryTag"/> only; the list id goes on the import span.
/// </remarks>
public static class DncRegistryTelemetry
{
    /// <summary>
    /// The area name of the meter and activity source.
    /// </summary>
    public const string Area = "DncRegistry";

    /// <summary>
    /// The tag that records which registry screened the numbers: <see cref="RegistryUsaFtc"/>,
    /// <see cref="RegistryCanadaDncl"/>, <see cref="RegistryLocal"/> or <see cref="CrestAppsOrchardCoreTelemetry.Other"/>.
    /// </summary>
    public const string RegistryTag = "registry";

    /// <summary>
    /// The tag that records which kind of work took a local list lock: <see cref="OperationImport"/> or
    /// <see cref="OperationDelete"/>.
    /// </summary>
    public const string OperationTag = CrestAppsOrchardCoreTelemetry.OperationTag;

    /// <summary>
    /// The United States FTC National Do Not Call Registry.
    /// </summary>
    public const string RegistryUsaFtc = "usa_ftc";

    /// <summary>
    /// The Canadian National Do Not Call List (LNNTE-DNCL).
    /// </summary>
    public const string RegistryCanadaDncl = "canada_dncl";

    /// <summary>
    /// The local do-not-call lists uploaded by administrators.
    /// </summary>
    public const string RegistryLocal = "local";

    /// <summary>
    /// The import of a local do-not-call list.
    /// </summary>
    public const string OperationImport = "import";

    /// <summary>
    /// The deletion of a local do-not-call list.
    /// </summary>
    public const string OperationDelete = "delete";

    /// <summary>
    /// The import run marked the list as completed.
    /// </summary>
    public const string ImportOutcomeCompleted = "completed";

    /// <summary>
    /// The import run stopped because the list was paused or is being deleted; it resumes from its saved row.
    /// </summary>
    public const string ImportOutcomeInterrupted = "interrupted";

    /// <summary>
    /// The import run failed and the list was marked as failed.
    /// </summary>
    public const string ImportOutcomeFailed = "failed";

    /// <summary>
    /// The reason a remote registry skipped its screening: its API key is not configured.
    /// </summary>
    public const string ReasonNotConfigured = "not_configured";

    /// <summary>
    /// The reason an import run failed: the uploaded file no longer exists.
    /// </summary>
    public const string ReasonFileMissing = "file_missing";

    /// <summary>
    /// The reason an import run failed: reading or saving the rows threw.
    /// </summary>
    public const string ReasonException = "exception";

    /// <summary>
    /// A row was saved to the list.
    /// </summary>
    public const string RowImported = "imported";

    /// <summary>
    /// A row repeated a number the list already holds.
    /// </summary>
    public const string RowDuplicate = "duplicate";

    /// <summary>
    /// A row did not hold a readable phone number.
    /// </summary>
    public const string RowInvalidNumber = "invalid_number";

    /// <summary>
    /// A row was blank.
    /// </summary>
    public const string RowBlank = "blank";

    /// <summary>
    /// A row held more than one value.
    /// </summary>
    public const string RowMultipleColumns = "multiple_columns";

    /// <summary>
    /// The first row was a header.
    /// </summary>
    public const string RowHeader = "header";

    /// <summary>
    /// The span of one screening call against one registry.
    /// </summary>
    public const string ScreeningSpanName = "crestapps.dnc.screening";

    /// <summary>
    /// The span of one run of the import of a local do-not-call list.
    /// </summary>
    public const string ImportRunSpanName = "crestapps.dnc.import.run";

    /// <summary>
    /// The span tag that holds the id of the local list an import works on. Spans only, never a metric.
    /// </summary>
    public const string ListIdSpanTag = "crestapps.dnc.list.id";

    /// <summary>
    /// The meter of the module, named <c>CrestApps.OrchardCore.DncRegistry</c>.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter(Area);

    /// <summary>
    /// The activity source of the module, named <c>CrestApps.OrchardCore.DncRegistry</c>.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource(Area);

    /// <summary>
    /// The duration of one screening call against one registry, tagged with <see cref="RegistryTag"/> and the outcome.
    /// </summary>
    public static Histogram<double> ScreeningDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dnc.screening.duration",
        "The duration of one screening of phone numbers against a do-not-call registry.");

    /// <summary>
    /// The number of phone numbers one screening call asked a registry about, tagged with <see cref="RegistryTag"/>.
    /// </summary>
    public static Histogram<long> ScreeningSize { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.dnc.screening.size",
        "{number}",
        "The number of phone numbers one screening asked a do-not-call registry about.");

    /// <summary>
    /// The number of screened phone numbers a registry reported as listed, tagged with <see cref="RegistryTag"/>.
    /// </summary>
    public static Counter<long> ScreeningListed { get; } = Meter.CreateCounter<long>(
        "crestapps.dnc.screening.listed",
        "{number}",
        "The number of screened phone numbers a do-not-call registry reported as listed.");

    /// <summary>
    /// The number of requests sent to a remote registry's API, tagged with <see cref="RegistryTag"/>, the outcome
    /// and the status class.
    /// </summary>
    public static Counter<long> RegistryRequests { get; } = Meter.CreateCounter<long>(
        "crestapps.dnc.registry.requests",
        "{request}",
        "The number of requests sent to a national do-not-call registry's API.");

    /// <summary>
    /// The duration of one request to a remote registry's API, tagged as <see cref="RegistryRequests"/> is.
    /// </summary>
    public static Histogram<double> RegistryRequestDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dnc.registry.request.duration",
        "The duration of one request to a national do-not-call registry's API.");

    /// <summary>
    /// The number of runs of the import of a local list, tagged with the outcome
    /// (<see cref="ImportOutcomeCompleted"/>, <see cref="ImportOutcomeInterrupted"/>, <see cref="ImportOutcomeFailed"/>,
    /// <c>skipped</c> or <c>cancelled</c>) and, for a failure, the reason.
    /// </summary>
    public static Counter<long> ImportRuns { get; } = Meter.CreateCounter<long>(
        "crestapps.dnc.import.runs",
        "{run}",
        "The number of runs of the import of a local do-not-call list, by how they ended.");

    /// <summary>
    /// The duration of one run of the import of a local list, tagged with the outcome.
    /// </summary>
    public static Histogram<double> ImportDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dnc.import.duration",
        "The duration of one run of the import of a local do-not-call list.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of rows of local list files the imports saved, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>. A row is counted once its batch is saved, so a resumed
    /// import does not count its rows twice.
    /// </summary>
    public static Counter<long> ImportRows { get; } = Meter.CreateCounter<long>(
        "crestapps.dnc.import.rows",
        "{row}",
        "The number of rows of local do-not-call list files the imports read, by what became of them.");

    /// <summary>
    /// The duration of saving one batch of numbers of a local list, without the wait for the shared pacing gate.
    /// </summary>
    public static Histogram<double> ImportBatchDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dnc.import.batch.duration",
        "The duration of saving one batch of numbers of a local do-not-call list.");

    /// <summary>
    /// The number of times the import or the deletion of a local list found its lock held by another worker, tagged
    /// with <see cref="OperationTag"/>.
    /// </summary>
    public static Counter<long> ImportLockContended { get; } = Meter.CreateCounter<long>(
        "crestapps.dnc.import.lock_contended",
        "{attempt}",
        "The number of times the import or the deletion of a local do-not-call list found its lock held by another worker.");

    /// <summary>
    /// Starts measuring one screening call against one registry. The caller records the numbers it checked with
    /// <see cref="RecordScreened(TelemetryScope, string, int, int)"/> and ends the scope with <c>Succeed</c>,
    /// <c>SetOutcome</c> or <c>Fail</c>.
    /// </summary>
    /// <param name="registry">The registry, such as <see cref="RegistryUsaFtc"/>.</param>
    /// <returns>The scope of the screening.</returns>
    public static TelemetryScope StartScreening(string registry)
    {
        return TelemetryScope.Start(
            ActivitySource,
            ScreeningSpanName,
            ScreeningDuration,
            new TagList
            {
                { RegistryTag, registry ?? CrestAppsOrchardCoreTelemetry.Other },
            });
    }

    /// <summary>
    /// Records how many numbers one screening call asked a registry about and how many were listed.
    /// </summary>
    /// <param name="screening">The scope of the screening.</param>
    /// <param name="registry">The registry.</param>
    /// <param name="checkedCount">The number of numbers the registry was asked about.</param>
    /// <param name="listedCount">The number of numbers it reported as listed.</param>
    public static void RecordScreened(TelemetryScope screening, string registry, int checkedCount, int listedCount)
    {
        ArgumentNullException.ThrowIfNull(screening);

        screening.SetSpanTag("crestapps.dnc.numbers.checked", checkedCount);
        screening.SetSpanTag("crestapps.dnc.numbers.listed", listedCount);

        if (!ScreeningSize.Enabled && !ScreeningListed.Enabled)
        {
            return;
        }

        var tags = CreateRegistryTags(registry);

        if (checkedCount > 0)
        {
            ScreeningSize.Record(checkedCount, tags);
        }

        if (listedCount > 0)
        {
            ScreeningListed.Add(listedCount, tags);
        }
    }

    /// <summary>
    /// Records one request to a remote registry's API.
    /// </summary>
    /// <param name="registry">The registry.</param>
    /// <param name="outcome">How the request ended.</param>
    /// <param name="statusCode">The HTTP status code, or <see langword="null"/> when no response was received.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch"/> timestamp taken before the request was sent.</param>
    public static void RecordRegistryRequest(string registry, string outcome, int? statusCode, long startTimestamp)
    {
        if (!RegistryRequests.Enabled && !RegistryRequestDuration.Enabled)
        {
            return;
        }

        var tags = CreateRegistryTags(registry);
        tags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome ?? CrestAppsOrchardCoreTelemetry.OutcomeError);
        tags.Add(CrestAppsOrchardCoreTelemetry.StatusClassTag, ExternalCallTelemetry.GetStatusClass(statusCode));

        RegistryRequests.Add(1, tags);
        RegistryRequestDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
    }

    /// <summary>
    /// Starts measuring one run of the import of a local list.
    /// </summary>
    /// <param name="listId">The id of the list, recorded on the span only.</param>
    /// <returns>The scope of the run; record the outcome with <c>SetOutcome</c>, <c>Fail</c> or <c>Succeed</c>.</returns>
    public static TelemetryScope StartImportRun(string listId)
    {
        var scope = TelemetryScope.Start(ActivitySource, ImportRunSpanName, ImportDuration);
        scope.SetSpanTag(ListIdSpanTag, listId);

        return scope;
    }

    /// <summary>
    /// Records how one run of the import of a local list ended.
    /// </summary>
    /// <param name="outcome">How the run ended.</param>
    /// <param name="reason">Why a failed run failed, from a bounded set, or <see langword="null"/>.</param>
    public static void RecordImportRun(string outcome, string reason = null)
    {
        if (!ImportRuns.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome ?? CrestAppsOrchardCoreTelemetry.OutcomeError },
        };

        if (!string.IsNullOrEmpty(reason))
        {
            tags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ImportRuns.Add(1, tags);
    }

    /// <summary>
    /// Records the duration of saving one batch of numbers of a local list.
    /// </summary>
    /// <param name="startTimestamp">The <see cref="Stopwatch"/> timestamp taken before the batch was saved.</param>
    /// <param name="saved">Whether the batch was saved.</param>
    public static void RecordImportBatch(long startTimestamp, bool saved)
    {
        if (!ImportBatchDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, saved ? CrestAppsOrchardCoreTelemetry.OutcomeOk : CrestAppsOrchardCoreTelemetry.OutcomeError },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ImportBatchDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
    }

    /// <summary>
    /// Records that the import or the deletion of a local list found its lock held by another worker.
    /// </summary>
    /// <param name="operation"><see cref="OperationImport"/> or <see cref="OperationDelete"/>.</param>
    public static void RecordLockContended(string operation)
    {
        if (!ImportLockContended.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { OperationTag, operation ?? CrestAppsOrchardCoreTelemetry.Other },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ImportLockContended.Add(1, tags);
    }

    private static TagList CreateRegistryTags(string registry)
    {
        var tags = new TagList
        {
            { RegistryTag, registry ?? CrestAppsOrchardCoreTelemetry.Other },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        return tags;
    }
}
