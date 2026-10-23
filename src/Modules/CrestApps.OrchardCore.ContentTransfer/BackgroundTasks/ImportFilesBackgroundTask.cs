using System.ComponentModel;
using System.Data;
using System.Data.Common;
using CrestApps.OrchardCore.BackgroundWork;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

[BackgroundTask(
    Title = "Imported Files Processor",
    Schedule = "* * * * *",
    Description = "Regularly check for imported files and process them, and resume imports that a restart interrupted.",
    LockTimeout = 3_000,
    LockExpiration = 30_000)]
public sealed class ImportFilesBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _importLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _importLockExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long one run works on an import before it saves its progress and gives up the lock. It leaves room under
    /// <see cref="RunLimits"/> for the batch in flight, which may first wait for the shared pacing gate.
    /// </summary>
    internal static readonly TimeSpan MaxRunDuration = _importLockExpiration - TimeSpan.FromMinutes(15);

    /// <summary>
    /// When the watchdog gives up on a run. A run past its time is stopped before its lock expires; a row that runs
    /// for minutes is reported, and one that runs far longer than any import row should is treated as stuck.
    /// </summary>
    internal static readonly ImportRunLimits RunLimits = new(
        RunTimeout: _importLockExpiration - TimeSpan.FromMinutes(2),
        RowTimeout: TimeSpan.FromMinutes(10),
        SlowRowThreshold: TimeSpan.FromMinutes(1),
        CheckInterval: TimeSpan.FromSeconds(15));

    /// <summary>
    /// How many runs may get stuck on the same row before the row is skipped and recorded as an error. One stuck run
    /// can be a passing outage; the same row stopping a second run is the row itself.
    /// </summary>
    internal const int MaxStalledRowAttempts = 2;

    /// <summary>
    /// How many runs in a row may fail at the same saved row, with no progress between them, before the import is
    /// marked as failed. A restart, a dropped connection or a deployment can fail the run that was working when it
    /// happened; marking the import failed on the first such error left it waiting for someone to resume it by hand.
    /// A failure that comes back run after run is the import itself, and it is then marked as failed with its reason.
    /// </summary>
    internal const int MaxFailedRunAttempts = 3;

    /// <summary>
    /// How many times in a row one scheduled run tries an import again after a failed run before it leaves the import to
    /// the next scheduled run.
    /// </summary>
    internal const int MaxRetriesInRun = 5;

    /// <summary>
    /// How long an import may go without saving progress before a run that finds its lock taken reports it.
    /// </summary>
    private static readonly TimeSpan _stalledProgressThreshold = TimeSpan.FromMinutes(10);

    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => ProcessEntriesAsync(serviceProvider, cancellationToken: cancellationToken);

    internal static async Task ProcessEntriesAsync(IServiceProvider serviceProvider, string entryId = null, CancellationToken cancellationToken = default)
    {
        // A run started from the admin pages gets no token of its own, so nothing told it the host was stopping. The
        // errors a stopping host throws at it, such as a database connection that is closed under it, then marked the
        // import as failed. The run is stopped with the host like the scheduled one, and the import resumes after the
        // restart.
        var applicationStopping = serviceProvider.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None;
        using var stoppingSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, applicationStopping);
        cancellationToken = stoppingSource.Token;

        var session = serviceProvider.GetRequiredService<ISession>();
        var distributedLock = serviceProvider.GetRequiredService<IDistributedLock>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<ImportFilesBackgroundTask>>();

        var entries = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
                (x.Status == ContentTransferEntryStatus.New
                    || x.Status == ContentTransferEntryStatus.Pending
                    || x.Status == ContentTransferEntryStatus.Processing)
                && x.Direction == ContentTransferDirection.Import
                && (entryId == null || x.EntryId == entryId))
            .OrderBy(x => x.CreatedUtc)
            .ListAsync(cancellationToken);

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // The lock cannot be extended once taken. An import that outlived it could be picked up by another
            // instance while this one was still working, and both would import the same rows. Each run therefore
            // stops before its lock expires, and the import continues under a fresh lock from its saved row.
            var continueImport = true;
            var failedRuns = 0;

            while (continueImport && !cancellationToken.IsCancellationRequested)
            {
                (var locker, var locked) = await distributedLock.TryAcquireLockAsync(
                    GetImportLockKey(entry.EntryId),
                    _importLockTimeout,
                    _importLockExpiration);

                if (!locked)
                {
                    LogLockTaken(logger, clock, entry);

                    break;
                }

                await using var acquiredLock = locker;

                var run = await ImportRunSupervisor.RunAsync(
                    (monitor, token) => ProcessEntryInScopeAsync(serviceProvider, entry.EntryId, monitor, token),
                    RunLimits,
                    logger,
                    entry.EntryId,
                    cancellationToken);

                if (run.Abandoned)
                {
                    if (run.StalledRowIndex > 0)
                    {
                        await RecordStalledRowAsync(serviceProvider, entry.EntryId, run.StalledRowIndex, logger, cancellationToken);
                    }

                    // The lock is released as this iteration ends. The next scheduled run resumes the import rather
                    // than this one at once, which gives whatever held the row time to recover.
                    break;
                }

                continueImport = run.Result == ImportEntryRunOutcome.Continue;

                if (run.Result == ImportEntryRunOutcome.RetryAfterFailure)
                {
                    // The run failed but left the import in Processing, as a timeout on a busy database does. Waiting
                    // for the next scheduled run left the import idle for as long as that took, however often it ran,
                    // and a busy database fails runs often. The import is tried again here after a pause that grows with
                    // each failure, and left to the next scheduled run once it has failed too often in a row.
                    failedRuns++;

                    if (failedRuns > MaxRetriesInRun)
                    {
                        break;
                    }

                    await RetryBackoff.DelayAsync(failedRuns, cancellationToken);

                    continueImport = true;
                }
                else
                {
                    failedRuns = 0;
                }
            }
        }
    }

    private static async Task<ImportEntryRunOutcome> ProcessEntryInScopeAsync(IServiceProvider serviceProvider, string entryId, ImportRunMonitor monitor, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        return await ProcessEntryAsync(scope.ServiceProvider, entryId, monitor, cancellationToken);
    }

    /// <summary>
    /// Reports an import whose lock another run holds. Finding the lock taken is normal while a run works through a
    /// large file; it is reported as a warning only once the import has gone a while without saving progress, which
    /// used to be the only sign of a run stuck on a row, and nothing was logged at all.
    /// </summary>
    private static void LogLockTaken(ILogger logger, IClock clock, ContentTransferEntry entry)
    {
        var lastSavedUtc = entry.ProcessSaveUtc ?? entry.CreatedUtc;

        if (clock.UtcNow - lastSavedUtc >= _stalledProgressThreshold)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "The import of entry '{EntryId}' has not saved progress since {LastSavedUtc:u}, and another run still holds its lock. That run is stopped once a row runs longer than {RowTimeout} or the run passes {RunTimeout}; the import then resumes from its last saved row.",
                    entry.EntryId,
                    lastSavedUtc,
                    RunLimits.RowTimeout,
                    RunLimits.RunTimeout);
            }

            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("The import of entry '{EntryId}' is already being processed by another run.", entry.EntryId);
        }
    }

    /// <summary>
    /// Remembers the row an abandoned run was stuck on, so a row that stops run after run is skipped instead of holding
    /// the import at the same place forever. The entry is saved with a concurrency check: if the abandoned run managed
    /// to save a batch after all, its progress stands and the row is not recorded.
    /// </summary>
    private static async Task RecordStalledRowAsync(IServiceProvider serviceProvider, string entryId, int rowIndex, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var entry = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId).FirstOrDefaultAsync(cancellationToken);

            if (entry == null || entry.Status.ShouldStopImport())
            {
                return;
            }

            var progressPart = entry.GetOrCreate<ImportFileProcessStatsPart>();
            RecordStalledRow(progressPart, rowIndex);
            entry.Put(progressPart);

            await session.SaveAsync(entry, true, collection: null, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Row {RowIndex} of the import of entry '{EntryId}' has now stopped {Attempts} of the {MaxAttempts} runs allowed before it is skipped.",
                    rowIndex,
                    entryId,
                    progressPart.StalledRowAttempts,
                    MaxStalledRowAttempts);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Could not record that the import of entry '{EntryId}' stopped on row {RowIndex}.", entryId, rowIndex);
            }
        }
    }

    internal static void RecordStalledRow(ImportFileProcessStatsPart progressPart, int rowIndex)
    {
        if (progressPart.StalledRow == rowIndex)
        {
            progressPart.StalledRowAttempts++;

            return;
        }

        progressPart.StalledRow = rowIndex;
        progressPart.StalledRowAttempts = 1;
    }

    internal static bool ShouldSkipStalledRow(ImportFileProcessStatsPart progressPart, int rowIndex)
        => progressPart.StalledRow == rowIndex
            && progressPart.StalledRowAttempts >= MaxStalledRowAttempts;

    /// <summary>
    /// Imports rows of one entry until the file is done, the import stops, or the run reaches
    /// <see cref="MaxRunDuration"/>. Returns <see cref="ImportEntryRunOutcome.Continue"/> only in the last case, when rows
    /// remain and the progress up to the last batch is saved.
    /// </summary>
    private static async Task<ImportEntryRunOutcome> ProcessEntryAsync(IServiceProvider serviceProvider, string entryId, ImportRunMonitor monitor, CancellationToken cancellationToken)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var contentManager = serviceProvider.GetRequiredService<IContentManager>();
        var contentImportOptions = serviceProvider.GetRequiredService<IOptions<ContentImportOptions>>().Value;
        var localizer = serviceProvider.GetRequiredService<IStringLocalizer<ImportFilesBackgroundTask>>();
        var fileStore = serviceProvider.GetRequiredService<IContentTransferFileStore>();
        var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();
        var contentImportManager = serviceProvider.GetRequiredService<IContentImportManager>();
        var rowFilters = serviceProvider.GetServices<IContentImportRowFilter>();
        var logger = serviceProvider.GetRequiredService<ILogger<ImportFilesBackgroundTask>>();
        var entry = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId).FirstOrDefaultAsync(cancellationToken);

        if (entry == null || entry.Status.ShouldStopImport())
        {
            return ImportEntryRunOutcome.Stop;
        }

        var runDeadlineUtc = clock.UtcNow + MaxRunDuration;
        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(entry.ContentType);

        if (contentTypeDefinition == null)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError("The import of entry '{EntryId}' failed because the content type '{ContentType}' no longer exists.", entry.EntryId, entry.ContentType);
            }

            await SaveEntryWithErrorAsync(session, clock, entry, localizer["The content definition was removed."], cancellationToken);
            return ImportEntryRunOutcome.Stop;
        }

        var fileInfo = await fileStore.GetFileInfoAsync(entry.StoredFileName);

        if (fileInfo == null || fileInfo.Length == 0)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError("The import of entry '{EntryId}' failed because its file '{StoredFileName}' is missing or empty in the content transfer file store.", entry.EntryId, entry.StoredFileName);
            }

            await SaveEntryWithErrorAsync(session, clock, entry, localizer["The import file no longer exists."], cancellationToken);
            return ImportEntryRunOutcome.Stop;
        }

        var batchSize = contentImportOptions.ImportBatchSize < 1
            ? ContentImportOptions.DefaultImportBatchSize
            : contentImportOptions.ImportBatchSize;

        var progressPart = entry.GetOrCreate<ImportFileProcessStatsPart>();
        progressPart.Errors ??= [];
        progressPart.ErrorMessages ??= [];
        progressPart.ErrorCategories ??= new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        var isResuming = progressPart.CurrentRow > 0;

        entry.Status = ContentTransferEntryStatus.Processing;
        entry.Error = null;
        entry.CompletedUtc = null;

        if (!isResuming)
        {
            progressPart.CurrentRow = 0;
            progressPart.TotalProcessed = 0;
            progressPart.ImportedCount = 0;
            progressPart.StalledRow = 0;
            progressPart.StalledRowAttempts = 0;
            progressPart.Errors.Clear();
            progressPart.ErrorMessages.Clear();
            progressPart.ErrorCategories.Clear();
        }

        entry.Put(progressPart);

        await session.SaveAsync(entry, false, collection: null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        var startRow = progressPart.CurrentRow;

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "The import of entry '{EntryId}' ('{FileName}') is {Action} after row {StartRow}.",
                entry.EntryId,
                entry.UploadedFileName,
                isResuming ? "resuming" : "starting",
                startRow);
        }

        ImportRunResult result;

        try
        {
            await using var fileStream = await fileStore.OpenSeekableReadStreamAsync(fileInfo, cancellationToken);

            var formatProviders = serviceProvider.GetServices<IContentTransferFileFormatProvider>()
                .OrderBy(provider => provider.FileExtension, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var formatProvider = formatProviders.FirstOrDefault(provider => provider.CanHandle(entry.StoredFileName))
                ?? throw new InvalidOperationException(localizer["Unsupported file format: {0}", Path.GetExtension(entry.StoredFileName)]);

            var filterInitContext = new ContentImportRowFilterInitContext()
            {
                ContentTypeDefinition = contentTypeDefinition,
                Entry = entry,
            };
            var activeRowFilters = new List<IContentImportRowFilter>();

            foreach (var filter in rowFilters)
            {
                if (await filter.InitializeAsync(filterInitContext))
                {
                    activeRowFilters.Add(filter);
                }
            }

            result = await ProcessFileInBatchesAsync(
                serviceProvider,
                fileStream,
                formatProvider,
                entry,
                progressPart,
                contentTypeDefinition,
                contentManager,
                contentImportManager,
                activeRowFilters,
                session,
                clock,
                batchSize,
                runDeadlineUtc,
                monitor,
                logger,
                localizer,
                cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // The host is stopping, or the watchdog gave up on this run and another may already have taken over. This
            // run records nothing; the entry stays in Processing and the next run resumes it from its saved row.
            return ImportEntryRunOutcome.Stop;
        }
        catch (Exception ex)
        {
            // The session that ran the import is unusable after a failed save, and saving the failure through it
            // fails too, which left the entry in Processing with no error and the same rows retried every run.
            return await RecordImportFailureAsync(serviceProvider, entry.EntryId, ex, localizer, cancellationToken)
                ? ImportEntryRunOutcome.RetryAfterFailure
                : ImportEntryRunOutcome.Stop;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ImportEntryRunOutcome.Stop;
        }

        if (result != ImportRunResult.Completed)
        {
            if (result == ImportRunResult.TimeLimitReached && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "The import of entry '{EntryId}' saved its progress at row {CurrentRow} of {TotalRecords} after its run time and continues under a fresh lock. This run started after row {StartRow}; {ImportedCount} rows are imported and {ErrorCount} have errors so far.",
                    entry.EntryId,
                    progressPart.CurrentRow,
                    progressPart.TotalRecords,
                    startRow,
                    progressPart.ImportedCount,
                    progressPart.Errors.Count);
            }
            else if (result == ImportRunResult.Stopped && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "The import of entry '{EntryId}' stopped at row {CurrentRow} of {TotalRecords} because it was paused or deleted. This run started after row {StartRow}; {ImportedCount} rows are imported and {ErrorCount} have errors so far.",
                    entry.EntryId,
                    progressPart.CurrentRow,
                    progressPart.TotalRecords,
                    startRow,
                    progressPart.ImportedCount,
                    progressPart.Errors.Count);
            }

            return result == ImportRunResult.TimeLimitReached
                ? ImportEntryRunOutcome.Continue
                : ImportEntryRunOutcome.Stop;
        }

        var nowUtc = clock.UtcNow;
        entry.ProcessSaveUtc = nowUtc;
        entry.CompletedUtc = nowUtc;
        entry.Status = progressPart.Errors.Count > 0
            ? ContentTransferEntryStatus.CompletedWithErrors
            : ContentTransferEntryStatus.Completed;
        entry.Put(progressPart);

        // An abandoned run must not mark the import complete over the run that replaced it.
        cancellationToken.ThrowIfCancellationRequested();

        await session.SaveAsync(entry, false, collection: null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "The import of entry '{EntryId}' finished with status {Status}: {TotalProcessed} of {TotalRecords} rows read, {ImportedCount} imported, {ErrorCount} with errors.",
                entry.EntryId,
                entry.Status,
                progressPart.TotalProcessed,
                progressPart.TotalRecords,
                progressPart.ImportedCount,
                progressPart.Errors.Count);
        }

        return ImportEntryRunOutcome.Stop;
    }

    private static async Task<ImportRunResult> ProcessFileInBatchesAsync(
        IServiceProvider serviceProvider,
        Stream stream,
        IContentTransferFileFormatProvider formatProvider,
        ContentTransferEntry entry,
        ImportFileProcessStatsPart progressPart,
        ContentTypeDefinition contentTypeDefinition,
        IContentManager contentManager,
        IContentImportManager contentImportManager,
        List<IContentImportRowFilter> activeRowFilters,
        ISession session,
        IClock clock,
        int batchSize,
        DateTime runDeadlineUtc,
        ImportRunMonitor monitor,
        ILogger logger,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        // Another system's layout, such as a store's product export, is read as the content type's columns.
        using var reader = await ContentTransferFileReaders.TransformAsync(
            serviceProvider.GetServices<IContentTransferFileReaderTransformer>(),
            () =>
            {
                stream.Position = 0;

                return formatProvider.CreateReader(stream);
            },
            contentTypeDefinition,
            cancellationToken);

        var pacingOptions = serviceProvider.GetService<IOptions<BackgroundWorkPacingOptions>>()?.Value;

        var columnNames = reader.GetColumnNames();
        using var dataTable = new DataTable();

        foreach (var columnName in columnNames)
        {
            var name = columnName;
            var occurrences = dataTable.Columns.Cast<DataColumn>().Count(c => c.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (occurrences > 0)
            {
                name += " " + (occurrences + 1);
            }

            dataTable.Columns.Add(new DataColumn(name));
        }

        progressPart.TotalRecords = reader.GetRowCount();

        var indexOfKeyColumn = dataTable.Columns.IndexOf(nameof(ContentItem.ContentItemId));
        var newRecords = new Dictionary<int, DataRow>();
        var existingRows = new Dictionary<string, KeyValuePair<int, DataRow>>(StringComparer.OrdinalIgnoreCase);
        var pendingRows = new List<ContentImportRowFilterContext>();

        var rowIndex = 1;
        var hasFilters = activeRowFilters.Count > 0;
        var importInterrupted = false;
        var firstRowOfRun = progressPart.CurrentRow;

        foreach (var rowValues in reader.ReadRows())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (rowIndex <= progressPart.CurrentRow)
            {
                rowIndex++;
                continue;
            }

            // Checked before every row, not only after a batch: rows a filter skips (a duplicate phone number, a
            // Do Not Call match) never fill a batch, so a file of mostly skipped rows could otherwise run on with
            // no check and no saved progress. The rows read since the last batch, with the reasons recorded for
            // skipped ones, are saved before the run stops, so the next run continues from this row.
            // The deadline counts from the start of the run, and reading the file and preparing the filters come
            // first. A run whose preparation took past the deadline used to stop before its first row, save nothing
            // new, and start over the same way for ever; each run now reads at least one row before it stops.
            if (clock.UtcNow >= runDeadlineUtc && progressPart.CurrentRow > firstRowOfRun)
            {
                await FilterPendingRowsAsync(pendingRows, activeRowFilters, contentTypeDefinition, entry, progressPart, indexOfKeyColumn, newRecords, existingRows, monitor, cancellationToken);

                var progressSaved = await BackgroundWorkPacer.RunBatchAsync(
                    token => ProcessBatchAsync(
                        serviceProvider,
                        entry,
                        dataTable,
                        newRecords,
                        existingRows,
                        contentTypeDefinition,
                        contentManager,
                        contentImportManager,
                        progressPart,
                        session,
                        clock,
                        monitor,
                        localizer,
                        token),
                    pacingOptions,
                    cancellationToken);

                return progressSaved
                    ? ImportRunResult.TimeLimitReached
                    : ImportRunResult.Stopped;
            }

            progressPart.TotalProcessed++;
            progressPart.CurrentRow = rowIndex;

            // A row that stopped one run after another would otherwise hold the import at the same place forever.
            if (ShouldSkipStalledRow(progressPart, rowIndex))
            {
                AddRowError(
                    progressPart,
                    rowIndex,
                    localizer["Importing this row stopped responding {0} times, so it was skipped.", progressPart.StalledRowAttempts],
                    localizer["Stopped responding"]);

                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        "Row {RowIndex} of the import of entry '{EntryId}' was skipped because importing it stopped responding {Attempts} times.",
                        rowIndex,
                        entry.EntryId,
                        progressPart.StalledRowAttempts);
                }

                rowIndex++;
                continue;
            }

            var dataRow = dataTable.NewRow();
            var isEmpty = true;

            for (var colIndex = 0; colIndex < rowValues.Length && colIndex < dataTable.Columns.Count; colIndex++)
            {
                var value = rowValues[colIndex];
                dataRow[colIndex] = value;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    isEmpty = false;
                }
            }

            if (!isEmpty)
            {
                var rowContext = new ContentImportRowFilterContext()
                {
                    Row = dataRow,
                    Columns = dataTable.Columns,
                    ContentTypeDefinition = contentTypeDefinition,
                    Entry = entry,
                    RowIndex = rowIndex,
                };

                if (hasFilters)
                {
                    // The rows are filtered a batch at a time, so a filter can look up what it needs for the whole
                    // batch at once instead of reading everything it might need when the import starts.
                    pendingRows.Add(rowContext);
                }
                else
                {
                    AddRowToBatch(rowContext, indexOfKeyColumn, newRecords, existingRows);
                }
            }

            var filtered = false;

            if (pendingRows.Count >= batchSize)
            {
                await FilterPendingRowsAsync(pendingRows, activeRowFilters, contentTypeDefinition, entry, progressPart, indexOfKeyColumn, newRecords, existingRows, monitor, cancellationToken);
                filtered = true;
            }

            // The rows a filtered batch keeps are imported at once, even when the filters skipped some of them.
            if (newRecords.Count + existingRows.Count >= batchSize ||
                (filtered && newRecords.Count + existingRows.Count > 0))
            {
                // Batches used to run back to back, holding the database at its limit for the whole import. The
                // batch runs through the gate every tenant's bulk work shares, which keeps that work to its share.
                var batchProcessed = await BackgroundWorkPacer.RunBatchAsync(
                    token => ProcessBatchAsync(
                        serviceProvider,
                        entry,
                        dataTable,
                        newRecords,
                        existingRows,
                        contentTypeDefinition,
                        contentManager,
                        contentImportManager,
                        progressPart,
                        session,
                        clock,
                        monitor,
                        localizer,
                        token),
                    pacingOptions,
                    cancellationToken);

                if (!batchProcessed)
                {
                    importInterrupted = true;
                    break;
                }

                newRecords.Clear();
                existingRows.Clear();
                dataTable.Rows.Clear();
            }

            rowIndex++;
        }

        if (importInterrupted || cancellationToken.IsCancellationRequested)
        {
            return ImportRunResult.Stopped;
        }

        await FilterPendingRowsAsync(pendingRows, activeRowFilters, contentTypeDefinition, entry, progressPart, indexOfKeyColumn, newRecords, existingRows, monitor, cancellationToken);

        if (newRecords.Count + existingRows.Count > 0)
        {
            var lastBatchProcessed = await ProcessBatchAsync(
                serviceProvider,
                entry,
                dataTable,
                newRecords,
                existingRows,
                contentTypeDefinition,
                contentManager,
                contentImportManager,
                progressPart,
                session,
                clock,
                monitor,
                localizer,
                cancellationToken);

            if (!lastBatchProcessed)
            {
                return ImportRunResult.Stopped;
            }
        }

        return ImportRunResult.Completed;
    }

    /// <summary>
    /// Imports one batch of rows and saves the progress. Returns <see langword="false"/> without importing
    /// anything when the entry was paused or deleted; its saved row is unchanged, so a resume reads the batch
    /// again. The status is checked once per batch: checking it before every row, including the rows a resume
    /// skips, cost one query per row of the file.
    /// </summary>
    private static async Task<bool> ProcessBatchAsync(
        IServiceProvider serviceProvider,
        ContentTransferEntry entry,
        DataTable dataTable,
        Dictionary<int, DataRow> newRecords,
        Dictionary<string, KeyValuePair<int, DataRow>> existingRows,
        ContentTypeDefinition contentTypeDefinition,
        IContentManager contentManager,
        IContentImportManager contentImportManager,
        ImportFileProcessStatsPart progressPart,
        ISession session,
        IClock clock,
        ImportRunMonitor monitor,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await ShouldStopImportAsync(serviceProvider, entry.EntryId, cancellationToken))
        {
            return false;
        }

        if (existingRows.Count > 0)
        {
            var existingContentItems = (await contentManager.GetAsync(existingRows.Keys, VersionOptions.DraftRequired))
                .ToDictionary(x => x.ContentItemId);

            foreach (var existingRow in existingRows)
            {
                monitor.BeginRow(existingRow.Value.Key);

                try
                {
                    var isNew = false;

                    if (!existingContentItems.TryGetValue(existingRow.Key, out var contentItem))
                    {
                        contentItem = await contentManager.NewAsync(entry.ContentType);
                        isNew = true;
                    }

                    await ProcessRowAsync(
                        entry,
                        contentItem,
                        existingRow.Value.Key,
                        existingRow.Value.Value,
                        dataTable.Columns,
                        contentTypeDefinition,
                        contentManager,
                        contentImportManager,
                        progressPart,
                        localizer,
                        isNew);
                }
                finally
                {
                    monitor.EndRow();
                }
            }
        }

        foreach (var record in newRecords)
        {
            monitor.BeginRow(record.Key);

            try
            {
                await ProcessRowAsync(
                    entry,
                    await contentManager.NewAsync(entry.ContentType),
                    record.Key,
                    record.Value,
                    dataTable.Columns,
                    contentTypeDefinition,
                    contentManager,
                    contentImportManager,
                    progressPart,
                    localizer,
                    true);
            }
            finally
            {
                monitor.EndRow();
            }
        }

        // The watchdog cancels a run it gave up on before it releases the lock, so a run that wakes up after another
        // took over stops here, and its uncommitted batch is rolled back instead of saved over the newer progress.
        cancellationToken.ThrowIfCancellationRequested();

        entry.ProcessSaveUtc = clock.UtcNow;
        entry.Put(progressPart);

        await session.SaveAsync(entry, false, collection: null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Runs the rows read since the last batch through the row filters and adds the rows they keep to the batch. Each
    /// filter first prepares for the rows together, then decides on each row in file order.
    /// </summary>
    internal static async Task FilterPendingRowsAsync(
        List<ContentImportRowFilterContext> pendingRows,
        IReadOnlyList<IContentImportRowFilter> rowFilters,
        ContentTypeDefinition contentTypeDefinition,
        ContentTransferEntry entry,
        ImportFileProcessStatsPart progressPart,
        int indexOfKeyColumn,
        Dictionary<int, DataRow> newRecords,
        Dictionary<string, KeyValuePair<int, DataRow>> existingRows,
        ImportRunMonitor monitor,
        CancellationToken cancellationToken)
    {
        if (pendingRows.Count == 0)
        {
            return;
        }

        // A filter may call out of the application, such as a do-not-call registry or the database, so the watchdog
        // watches this work as it watches the import of a row.
        monitor?.BeginRow(pendingRows[0].RowIndex);

        try
        {
            var batchContext = new ContentImportRowFilterBatchContext()
            {
                ContentTypeDefinition = contentTypeDefinition,
                Entry = entry,
                Rows = pendingRows,
                CancellationToken = cancellationToken,
            };

            foreach (var filter in rowFilters)
            {
                await filter.PrepareBatchAsync(batchContext);
            }
        }
        finally
        {
            monitor?.EndRow();
        }

        foreach (var rowContext in pendingRows)
        {
            var shouldSkip = false;

            monitor?.BeginRow(rowContext.RowIndex);

            try
            {
                foreach (var filter in rowFilters)
                {
                    if (await filter.ShouldSkipRowAsync(rowContext))
                    {
                        shouldSkip = true;
                        break;
                    }
                }
            }
            finally
            {
                monitor?.EndRow();
            }

            if (!shouldSkip)
            {
                AddRowToBatch(rowContext, indexOfKeyColumn, newRecords, existingRows);
            }
            else if (!string.IsNullOrWhiteSpace(rowContext.SkipReason))
            {
                AddRowError(progressPart, rowContext.RowIndex, rowContext.SkipReason, rowContext.SkipCategory);
            }
        }

        pendingRows.Clear();
    }

    private static void AddRowToBatch(
        ContentImportRowFilterContext rowContext,
        int indexOfKeyColumn,
        Dictionary<int, DataRow> newRecords,
        Dictionary<string, KeyValuePair<int, DataRow>> existingRows)
    {
        var contentItemId = GetImportContentItemId(rowContext.Row, indexOfKeyColumn);

        if (!string.IsNullOrEmpty(contentItemId))
        {
            existingRows[contentItemId] = new KeyValuePair<int, DataRow>(rowContext.RowIndex, rowContext.Row);
        }
        else
        {
            newRecords[rowContext.RowIndex] = rowContext.Row;
        }
    }

    internal static string GetImportContentItemId(DataRow dataRow, int indexOfKeyColumn)
        => indexOfKeyColumn > -1
            ? dataRow[indexOfKeyColumn]?.ToString()?.Trim()
            : null;

    private static async Task ProcessRowAsync(
        ContentTransferEntry entry,
        ContentItem contentItem,
        int rowIndex,
        DataRow row,
        DataColumnCollection columns,
        ContentTypeDefinition contentTypeDefinition,
        IContentManager contentManager,
        IContentImportManager contentImportManager,
        ImportFileProcessStatsPart progressPart,
        IStringLocalizer localizer,
        bool isNew)
    {
        try
        {
            var importOptions = entry.GetOrCreate<ImportContentOptionsPart>();
            var mapContext = new ContentImportContext()
            {
                ContentItem = contentItem,
                ContentTypeDefinition = contentTypeDefinition,
                Entry = entry,
                Columns = columns,
                Row = row,
            };

            await contentImportManager.ImportAsync(mapContext);

            var validationResult = await contentManager.ValidateAsync(mapContext.ContentItem);

            if (!validationResult.Succeeded)
            {
                AddRowError(progressPart, rowIndex, FormatValidationErrors(validationResult), localizer["Did not pass validation"]);
                return;
            }

            mapContext.ContentItem.Owner = entry.Owner;
            mapContext.ContentItem.Author = entry.Author;

            if (isNew)
            {
                await contentManager.CreateAsync(mapContext.ContentItem, VersionOptions.DraftRequired);

                if (importOptions.PublishImportedContent)
                {
                    await contentManager.PublishAsync(mapContext.ContentItem);
                }
            }
            else
            {
                await contentManager.UpdateAsync(mapContext.ContentItem);

                if (importOptions.PublishImportedContent)
                {
                    await contentManager.PublishAsync(mapContext.ContentItem);
                }
                else
                {
                    await contentManager.SaveDraftAsync(mapContext.ContentItem);
                }
            }

            progressPart.ImportedCount++;

            RemoveRowError(progressPart, rowIndex);
        }
        catch (Exception ex)
        {
            AddRowError(progressPart, rowIndex, ex.Message, localizer["Could not be imported"]);
        }
    }

    /// <summary>
    /// Records a failed row. Every failed row keeps its <paramref name="category"/>, so the rows that failed for each
    /// reason are counted however many fail, while its <paramref name="errorMessage"/> is kept for the first
    /// <see cref="ImportFileProcessStatsPart.MaxStoredErrorMessages"/> failed rows only.
    /// </summary>
    internal static void AddRowError(ImportFileProcessStatsPart progressPart, int rowIndex, string errorMessage, string category = null)
    {
        progressPart.Errors ??= [];
        progressPart.ErrorMessages ??= [];
        progressPart.ErrorCategories ??= new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        progressPart.Errors.Add(rowIndex);

        // A row read again after a resume may fail for another reason than the first time.
        RemoveRowFromCategories(progressPart, rowIndex);

        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!progressPart.ErrorCategories.TryGetValue(category, out var rows))
            {
                rows = [];
                progressPart.ErrorCategories[category] = rows;
            }

            rows.Add(rowIndex);
        }

        if (progressPart.ErrorMessages.Count >= ImportFileProcessStatsPart.MaxStoredErrorMessages
            && !progressPart.ErrorMessages.ContainsKey(rowIndex))
        {
            return;
        }

        progressPart.ErrorMessages[rowIndex] = string.IsNullOrWhiteSpace(errorMessage)
            ? "The row failed to import."
            : errorMessage;
    }

    /// <summary>
    /// Forgets the error of a row that was imported after all.
    /// </summary>
    internal static void RemoveRowError(ImportFileProcessStatsPart progressPart, int rowIndex)
    {
        progressPart.Errors?.Remove(rowIndex);
        progressPart.ErrorMessages?.Remove(rowIndex);
        RemoveRowFromCategories(progressPart, rowIndex);
    }

    private static void RemoveRowFromCategories(ImportFileProcessStatsPart progressPart, int rowIndex)
    {
        if (progressPart.ErrorCategories == null)
        {
            return;
        }

        // A row is in one category at most.
        foreach (var (category, rows) in progressPart.ErrorCategories)
        {
            if (!rows.Remove(rowIndex))
            {
                continue;
            }

            if (rows.Count == 0)
            {
                progressPart.ErrorCategories.Remove(category);
            }

            return;
        }
    }

    private static string FormatValidationErrors(ContentValidateResult validationResult)
    {
        var messages = validationResult.Errors
            .Select(error =>
            {
                if (error.MemberNames != null && error.MemberNames.Any())
                {
                    return $"{string.Join(", ", error.MemberNames)}: {error.ErrorMessage}";
                }

                return error.ErrorMessage;
            })
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return messages.Length > 0
            ? string.Join("; ", messages)
            : "The row failed validation.";
    }

    private static async Task SaveEntryWithErrorAsync(ISession session, IClock clock, ContentTransferEntry entry, string error, CancellationToken cancellationToken)
    {
        entry.Status = ContentTransferEntryStatus.Failed;
        entry.Error = error;
        entry.ProcessSaveUtc = clock.UtcNow;
        entry.CompletedUtc = clock.UtcNow;

        await session.SaveAsync(entry, false, collection: null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Records why an import stopped, in a scope of its own. A transient database failure, such as a timeout on a
    /// busy database, keeps the entry in Processing so the next run resumes it from its last saved row; any other
    /// failure marks it as failed with the reason, and the user can resume it once the cause is fixed. The entry
    /// is loaded again rather than reused, so the rows of the failed batch are not recorded as done.
    /// </summary>
    private static async Task<bool> RecordImportFailureAsync(
        IServiceProvider serviceProvider,
        string entryId,
        Exception exception,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ImportFilesBackgroundTask>>();
        var isTransient = IsTransient(exception);

        if (isTransient && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                exception,
                "The import of entry '{EntryId}' stopped because a call it made, to the database or to a service, did not respond in time. It will resume from its last saved row on the next run.",
                entryId);
        }
        else if (!isTransient && logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(exception, "The import of entry '{EntryId}' failed and was marked as failed.", entryId);
        }

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var entry = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId).FirstOrDefaultAsync(cancellationToken);

            if (entry == null || entry.Status.ShouldStopImport())
            {
                return false;
            }

            if (isTransient)
            {
                entry.Error = localizer["A call the import made did not respond in time. The import will resume automatically: {0}", exception.Message];
                entry.ProcessSaveUtc = clock.UtcNow;

                await session.SaveAsync(entry, false, collection: null, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                return true;
            }

            var progressPart = entry.GetOrCreate<ImportFileProcessStatsPart>();

            if (!RecordFailedRun(progressPart))
            {
                // The failure may have been the restart or the connection rather than the import, so the import keeps
                // resuming on the next run until the same failure has stopped it from making progress a few times.
                entry.Put(progressPart);
                entry.Error = localizer["A run of the import failed and it will resume automatically: {0}", exception.Message];
                entry.ProcessSaveUtc = clock.UtcNow;

                await session.SaveAsync(entry, false, collection: null, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        exception,
                        "A run of the import of entry '{EntryId}' failed at row {Row}, failure {Attempts} of the {MaxAttempts} allowed before the import is marked as failed. It will resume from its last saved row on the next run.",
                        entryId,
                        progressPart.FailedRunRow,
                        progressPart.FailedRunAttempts,
                        MaxFailedRunAttempts);
                }

                return true;
            }

            await SaveEntryWithErrorAsync(session, clock, entry, localizer["Error processing file: {0}", exception.Message], cancellationToken);

            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The database may still be unavailable. The entry then stays in Processing and the next run resumes it.
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Could not record why the import of entry '{EntryId}' stopped.", entryId);
            }

            return true;
        }
    }

    /// <summary>
    /// Counts a failed run against the row it stopped at. A run that fails at the same saved row as the one before
    /// it, with no batch saved between them, adds to the count; a run that failed further on starts it over.
    /// </summary>
    /// <returns><see langword="true"/> when the import has now failed too many runs in a row to keep resuming.</returns>
    internal static bool RecordFailedRun(ImportFileProcessStatsPart progressPart)
    {
        if (progressPart.FailedRunRow == progressPart.CurrentRow && progressPart.FailedRunAttempts > 0)
        {
            progressPart.FailedRunAttempts++;
        }
        else
        {
            progressPart.FailedRunRow = progressPart.CurrentRow;
            progressPart.FailedRunAttempts = 1;
        }

        return progressPart.FailedRunAttempts >= MaxFailedRunAttempts;
    }

    internal static bool IsTransient(Exception exception)
        => exception switch
        {
            null => false,
            TimeoutException => true,

            // A command timeout surfaces as a database exception wrapping the wait timeout (native error 258).
            DbException { IsTransient: true } => true,
            Win32Exception { NativeErrorCode: 258 } => true,
            _ => IsTransient(exception.InnerException),
        };

    /// <summary>
    /// What a run of one import asks of the loop that started it.
    /// </summary>
    internal enum ImportEntryRunOutcome
    {
        /// <summary>The import is finished, stopped, or failed; nothing more to do now.</summary>
        Stop,

        /// <summary>The run reached its time limit with rows left; run again under a fresh lock.</summary>
        Continue,

        /// <summary>The run failed but the import is still in Processing; run again after a pause.</summary>
        RetryAfterFailure,
    }

    /// <summary>
    /// The pause before an import is tried again after a failed run.
    /// </summary>
    internal static class RetryBackoff
    {
        private static readonly TimeSpan _first = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan _longest = TimeSpan.FromMinutes(2);

        public static TimeSpan GetDelay(int failedRuns)
        {
            var seconds = _first.TotalSeconds * Math.Pow(2, Math.Max(0, failedRuns - 1));

            return TimeSpan.FromSeconds(Math.Min(seconds, _longest.TotalSeconds));
        }

        public static Task DelayAsync(int failedRuns, CancellationToken cancellationToken)
            => Task.Delay(GetDelay(failedRuns), cancellationToken);
    }

    private enum ImportRunResult
    {
        Completed,
        Stopped,
        TimeLimitReached,
    }

    internal static string GetImportLockKey(string entryId)
        => $"ContentsTransfer_Import_{entryId}";

    private static async Task<bool> ShouldStopImportAsync(IServiceProvider serviceProvider, string entryId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        var currentEntry = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId).FirstOrDefaultAsync(cancellationToken);

        return currentEntry?.Status.ShouldStopImport() == true;
    }
}
