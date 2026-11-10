using System.Data;
using System.Diagnostics;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Entities;
using OrchardCore.FileStorage;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

[BackgroundTask(
    Title = "Export Files Processor",
    Schedule = "*/5 * * * *",
    Description = "Regularly check for queued export requests and process them.",
    LockTimeout = 3_000, LockExpiration = 30_000)]
public sealed class ExportFilesBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _exportLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _exportLockExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _progressLogInterval = TimeSpan.FromMinutes(1);

    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Each export runs under a lock of its own that outlives the task's lock by far, so the run is not measured
        // against the task's lock expiration.
        return BackgroundTaskTelemetry.RunAsync(
            nameof(ExportFilesBackgroundTask),
            run => ProcessEntriesAsync(serviceProvider, entryId: null, run, cancellationToken),
            lockExpiration: null);
    }

    internal static Task ProcessEntriesAsync(IServiceProvider serviceProvider, string entryId = null, CancellationToken cancellationToken = default)
    {
        return ProcessEntriesAsync(serviceProvider, entryId, taskRun: null, cancellationToken);
    }

    private static async Task ProcessEntriesAsync(
        IServiceProvider serviceProvider,
        string entryId,
        MeasuredTaskRun taskRun,
        CancellationToken cancellationToken)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var distributedLock = serviceProvider.GetRequiredService<IDistributedLock>();
        var contentImportOptions = serviceProvider.GetRequiredService<IOptions<ContentImportOptions>>().Value;
        var logger = serviceProvider.GetRequiredService<ILogger<ExportFilesBackgroundTask>>();

        var entries = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
            (x.Status == ContentTransferEntryStatus.New || x.Status == ContentTransferEntryStatus.Processing)
            && x.Direction == ContentTransferDirection.Export
            && (entryId == null || x.EntryId == entryId))
        .OrderBy(x => x.CreatedUtc)
        .ListAsync(cancellationToken);

        if (!entries.Any())
        {
            taskRun?.Skip();

            return;
        }

        var batchSize = contentImportOptions.ExportBatchSize < 1
            ? ContentImportOptions.DefaultExportBatchSize
            : contentImportOptions.ExportBatchSize;

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            (var locker, var locked) = await distributedLock.TryAcquireLockAsync(
                GetExportLockKey(entry.EntryId),
                _exportLockTimeout,
                _exportLockExpiration);

            if (!locked)
            {
                taskRun?.AddItems(1, "lock_contended");

                if (logger.IsEnabled(LogLevel.Information))
                {
                    ContentTransferLog.ExportLockTaken(logger, entry.EntryId);
                }

                continue;
            }

            await using var acquiredLock = locker;

            using var logScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["EntryId"] = entry.EntryId,
                ["ContentType"] = entry.ContentType,
                ["Direction"] = entry.Direction.ToString(),
            });

            var outcome = await ExportEntryAsync(serviceProvider, session, entry, batchSize, logger, cancellationToken);

            taskRun?.AddItems(1, outcome);
        }
    }

    /// <summary>
    /// Exports one entry, measured as the <c>crestapps.contenttransfer.export.run</c> span linked to the request that
    /// queued it.
    /// </summary>
    /// <returns>How the export ended: <c>ok</c>, <c>error</c> or <c>cancelled</c>.</returns>
    private static async Task<string> ExportEntryAsync(
        IServiceProvider serviceProvider,
        ISession session,
        ContentTransferEntry entry,
        int batchSize,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var clock = serviceProvider.GetRequiredService<IClock>();
        var localizer = serviceProvider.GetRequiredService<IStringLocalizer<ExportFilesBackgroundTask>>();
        var fileStore = serviceProvider.GetRequiredService<IContentTransferFileStore>();
        var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();
        var contentImportManager = serviceProvider.GetRequiredService<IContentImportManager>();
        var tempDirectoryProvider = serviceProvider.GetRequiredService<ITempDirectoryProvider>();
        var format = ContentTransferDiagnostics.GetFormat(entry.StoredFileName);

        using var telemetry = TelemetryScope.StartLinked(
            ContentTransferDiagnostics.ActivitySource,
            "crestapps.contenttransfer.export.run",
            ContentTransferDiagnostics.ExportDuration,
            ContentTransferDiagnostics.GetTraceParent(entry),
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.FormatTag, format },
                { ContentTransferDiagnostics.ContentTypeTag, entry.ContentType },
            });

        telemetry.SetSpanTag(ContentTransferDiagnostics.EntryIdSpanTag, entry.EntryId);

        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(entry.ContentType);

        if (contentTypeDefinition == null)
        {
            await SaveEntryWithErrorAsync(session, clock, entry, localizer["The content definition was removed."]);
            FailExport(telemetry, logger, entry, "content_type_removed");

            return telemetry.Outcome;
        }

        entry.Status = ContentTransferEntryStatus.Processing;

        var progressPart = entry.GetOrCreate<ImportFileProcessStatsPart>();
        progressPart.Errors ??= [];

        try
        {
            var context = new ImportContentContext()
            {
                ContentItem = await serviceProvider.GetRequiredService<IContentManager>().NewAsync(entry.ContentType),
                ContentTypeDefinition = contentTypeDefinition,
                Entry = entry,
            };

            var columns = await contentImportManager.GetColumnsAsync(context);
            var exportColumns = columns.Where(x => x.Type != ImportColumnType.ImportOnly).ToList();

            // Count total records for progress tracking.
            var hasFilters = entry.TryGet<ExportFilterPart>(out var filters);

            var totalCountQuery = BuildExportQuery(
                session, entry.ContentType, hasFilters,
                filters?.LatestOnly ?? false,
                filters?.AllVersions ?? false,
                filters?.CreatedFrom, filters?.CreatedTo,
                filters?.ModifiedFrom, filters?.ModifiedTo,
                filters?.Owners);

            var totalCount = await totalCountQuery.CountAsync(cancellationToken);

            progressPart.TotalRecords = totalCount;

            // Resolve file format provider from the stored file name extension.
            var formatProviders = serviceProvider.GetServices<IContentTransferFileFormatProvider>()
                .OrderBy(provider => provider.FileExtension, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var formatProvider = formatProviders.FirstOrDefault(provider => provider.CanHandle(entry.StoredFileName))
                ?? formatProviders.FirstOrDefault();

            if (formatProvider == null)
            {
                await SaveEntryWithErrorAsync(session, clock, entry, localizer["No file formats are currently enabled."]);
                FailExport(telemetry, logger, entry, "no_format");

                return telemetry.Outcome;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                ContentTransferLog.ExportStarted(logger, entry.EntryId, entry.ContentType, totalCount);
            }

            var fileName = entry.StoredFileName;
            var tempFilePath = tempDirectoryProvider.GetTempFileName();

            using var tempStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 4096, FileOptions.DeleteOnClose);
            var columnNames = exportColumns.Select(c => c.Name).ToList();
            var lastProgressLogTimestamp = Stopwatch.GetTimestamp();

            using (var writer = formatProvider.CreateWriter(tempStream, contentTypeDefinition.DisplayName))
            {
                writer.WriteHeader(columnNames);

                // Paginate content items and write each page directly.
                var page = 0;
                var contentManager = serviceProvider.GetRequiredService<IContentManager>();

                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    using var pageTelemetry = TelemetryScope.Start(
                        ContentTransferDiagnostics.ActivitySource,
                        "crestapps.contenttransfer.export.page",
                        ContentTransferDiagnostics.ExportPageDuration,
                        new TagList
                        {
                            { ContentTransferDiagnostics.ModeTag, ContentTransferDiagnostics.ModeQueued },
                        });

                    var pageQuery = BuildExportQuery(
                        session, entry.ContentType, hasFilters,
                        filters?.LatestOnly ?? false,
                        filters?.AllVersions ?? false,
                        filters?.CreatedFrom, filters?.CreatedTo,
                        filters?.ModifiedFrom, filters?.ModifiedTo,
                        filters?.Owners);

                    var contentItems = await pageQuery
                        .Skip(page * batchSize)
                        .Take(batchSize)
                        .ListAsync(cancellationToken);

                    var items = contentItems.ToList();

                    if (items.Count == 0)
                    {
                        pageTelemetry.Succeed();

                        break;
                    }

                    pageTelemetry.SetSpanTag(ContentTransferDiagnostics.RowsSpanTag, items.Count);

                    // Let handlers pre-load per-page data in a single pass before mapping rows.
                    await contentImportManager.PrepareExportBatchAsync(new ContentExportBatchContext
                    {
                        ContentItems = items,
                        ContentTypeDefinition = contentTypeDefinition,
                        Entry = entry,
                    });

                    // Create a temporary DataTable for this batch only.
                    using var dataTable = new DataTable();

                    foreach (var colName in columnNames)
                    {
                        dataTable.Columns.Add(colName);
                    }

                    var written = 0;
                    var excluded = 0;

                    foreach (var contentItem in items)
                    {
                        var mapContext = new ContentExportContext()
                        {
                            ContentItem = contentItem,
                            ContentTypeDefinition = contentTypeDefinition,
                            Entry = entry,
                            Row = dataTable.NewRow(),
                        };

                        await contentImportManager.ExportAsync(mapContext);

                        progressPart.TotalProcessed++;

                        // A handler may exclude a content item from the output (for example, a contributed
                        // "only rows that match" filter). Count it as processed but do not write it.
                        if (mapContext.Exclude)
                        {
                            excluded++;

                            continue;
                        }

                        var rowValues = new List<string>(columnNames.Count);
                        foreach (var colName in columnNames)
                        {
                            rowValues.Add(mapContext.Row[colName]?.ToString() ?? string.Empty);
                        }

                        writer.WriteRow(rowValues);

                        // Track how many records were actually written so the UI can show the exported total.
                        progressPart.ImportedCount++;
                        written++;
                    }

                    // Save progress after each page.
                    progressPart.CurrentRow = progressPart.TotalProcessed;
                    entry.ProcessSaveUtc = clock.UtcNow;
                    entry.Put(progressPart);

                    await session.SaveAsync(entry, false, collection: null, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);

                    RecordPage(ContentTransferDiagnostics.ModeQueued, written, excluded);
                    pageTelemetry.Succeed();

                    if (Stopwatch.GetElapsedTime(lastProgressLogTimestamp) >= _progressLogInterval && logger.IsEnabled(LogLevel.Information))
                    {
                        lastProgressLogTimestamp = Stopwatch.GetTimestamp();
                        ContentTransferLog.ExportProgress(logger, entry.EntryId, progressPart.TotalProcessed, progressPart.TotalRecords);
                    }

                    page++;
                }

                writer.Flush();
            }

            var fileSize = tempStream.Length;

            // Save the completed file to the file store.
            tempStream.Seek(0, SeekOrigin.Begin);
            await fileStore.CreateFileFromStreamAsync(fileName, tempStream, true);

            var nowUtc = clock.UtcNow;
            entry.ProcessSaveUtc = nowUtc;
            entry.CompletedUtc = nowUtc;
            entry.Status = ContentTransferEntryStatus.Completed;
            entry.Put(progressPart);

            await session.SaveAsync(entry, false, collection: null, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            ContentTransferDiagnostics.Record(ContentTransferDiagnostics.ExportFileSize, fileSize, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.FormatTag, format },
                { ContentTransferDiagnostics.ModeTag, ContentTransferDiagnostics.ModeQueued },
            });

            telemetry.Succeed();

            if (logger.IsEnabled(LogLevel.Information))
            {
                ContentTransferLog.ExportFinished(logger, entry.EntryId, telemetry.Elapsed, progressPart.ImportedCount, progressPart.TotalProcessed, fileSize);
            }

            // Send notification if the Notifications module is enabled.
            await TrySendExportNotificationAsync(serviceProvider, entry, contentTypeDefinition.DisplayName);
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            RecordExportFailure("exception");

            if (logger.IsEnabled(LogLevel.Error))
            {
                ContentTransferLog.ExportFailed(logger, ex, entry.EntryId);
            }

            await SaveEntryWithErrorAsync(session, clock, entry, localizer["Error processing export: {0}", ex.Message]);
        }

        return telemetry.Outcome;
    }

    private static void FailExport(TelemetryScope telemetry, ILogger logger, ContentTransferEntry entry, string reason)
    {
        telemetry.Fail(reason);
        RecordExportFailure(reason);

        if (logger.IsEnabled(LogLevel.Error))
        {
            ContentTransferLog.ExportEntryFailed(logger, entry.EntryId, reason);
        }
    }

    private static void RecordExportFailure(string reason)
    {
        ContentTransferDiagnostics.Add(ContentTransferDiagnostics.ExportFailures, 1, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
            { ContentTransferDiagnostics.ModeTag, ContentTransferDiagnostics.ModeQueued },
        });
    }

    /// <summary>
    /// Records one page of an export on <c>crestapps.contenttransfer.export.pages</c> and
    /// <c>crestapps.contenttransfer.export.rows</c>.
    /// </summary>
    /// <param name="mode">How the export runs: <c>queued</c> or <c>inline</c>.</param>
    /// <param name="written">The content items written to the file.</param>
    /// <param name="excluded">The content items a handler excluded from the file.</param>
    internal static void RecordPage(string mode, int written, int excluded)
    {
        ContentTransferDiagnostics.Add(ContentTransferDiagnostics.ExportPages, 1, ContentTransferDiagnostics.ModeTag, mode);
        ContentTransferDiagnostics.Add(ContentTransferDiagnostics.ExportRows, written, new TagList
        {
            { ContentTransferDiagnostics.ModeTag, mode },
            { CrestAppsOrchardCoreTelemetry.ResultTag, ContentTransferDiagnostics.ResultWritten },
        });

        ContentTransferDiagnostics.Add(ContentTransferDiagnostics.ExportRows, excluded, new TagList
        {
            { ContentTransferDiagnostics.ModeTag, mode },
            { CrestAppsOrchardCoreTelemetry.ResultTag, ContentTransferDiagnostics.ResultExcluded },
        });
    }

    private static async Task TrySendExportNotificationAsync(
        IServiceProvider serviceProvider,
        ContentTransferEntry entry,
        string contentTypeName)
    {
        // Resolve IContentTransferNotificationHandler if available (registered when Notifications module is enabled).
        var notificationHandler = serviceProvider.GetService<IContentTransferNotificationHandler>();

        if (notificationHandler != null)
        {
            await notificationHandler.NotifyExportCompletedAsync(entry, contentTypeName);
        }
    }

    private static async Task SaveEntryWithErrorAsync(ISession session, IClock clock, ContentTransferEntry entry, string error)
    {
        entry.Status = ContentTransferEntryStatus.Failed;
        entry.Error = error;
        entry.CompletedUtc = clock.UtcNow;

        await session.SaveAsync(entry, false, collection: null);
        await session.SaveChangesAsync();
    }

    internal static string GetExportLockKey(string entryId)
        => $"ContentsTransfer_Export_{entryId}";

    private static IQuery<ContentItem> BuildExportQuery(
        ISession session,
        string contentTypeId,
        bool hasFilters,
        bool latestOnly,
        bool allVersions,
        DateTime? createdFrom,
        DateTime? createdTo,
        DateTime? modifiedFrom,
        DateTime? modifiedTo,
        string owners)
    {
        IQuery<ContentItem, ContentItemIndex> query;

        if (allVersions)
        {
            query = session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentTypeId);
        }
        else if (latestOnly)
        {
            query = session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentTypeId && x.Latest);
        }
        else
        {
            query = session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentTypeId && x.Published);
        }

        if (hasFilters)
        {
            if (createdFrom.HasValue)
            {
                query = query.Where(x => x.CreatedUtc >= createdFrom.Value);
            }

            if (createdTo.HasValue)
            {
                query = query.Where(x => x.CreatedUtc <= createdTo.Value);
            }

            if (modifiedFrom.HasValue)
            {
                query = query.Where(x => x.ModifiedUtc >= modifiedFrom.Value);
            }

            if (modifiedTo.HasValue)
            {
                query = query.Where(x => x.ModifiedUtc <= modifiedTo.Value);
            }

            if (!string.IsNullOrWhiteSpace(owners))
            {
                var ownerList = owners.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (ownerList.Length == 1)
                {
                    var owner = ownerList[0];
                    query = query.Where(x => x.Owner == owner);
                }
                else if (ownerList.Length > 1)
                {
                    var o0 = ownerList[0];
                    var o1 = ownerList.ElementAtOrDefault(1) ?? o0;
                    var o2 = ownerList.ElementAtOrDefault(2) ?? o0;
                    var o3 = ownerList.ElementAtOrDefault(3) ?? o0;
                    var o4 = ownerList.ElementAtOrDefault(4) ?? o0;

                    query = query.Where(x =>
                        x.Owner == o0 || x.Owner == o1 || x.Owner == o2 ||
                        x.Owner == o3 || x.Owner == o4);
                }
            }
        }

        return query.OrderBy(x => x.CreatedUtc);
    }
}
