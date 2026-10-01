using System.ComponentModel;
using System.Data;
using System.Data.Common;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.Extensions.DependencyInjection;
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
    Schedule = "*/10 * * * *",
    Description = "Regularly check for imported files and process them.",
    LockTimeout = 3_000,
    LockExpiration = 30_000)]
public sealed class ImportFilesBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _importLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _importLockExpiration = TimeSpan.FromMinutes(30);

    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => ProcessEntriesAsync(serviceProvider, cancellationToken: cancellationToken);

    internal static async Task ProcessEntriesAsync(IServiceProvider serviceProvider, string entryId = null, CancellationToken cancellationToken = default)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var distributedLock = serviceProvider.GetRequiredService<IDistributedLock>();

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

            (var locker, var locked) = await distributedLock.TryAcquireLockAsync(
                GetImportLockKey(entry.EntryId),
                _importLockTimeout,
                _importLockExpiration);

            if (!locked)
            {
                continue;
            }

            await using var acquiredLock = locker;
            await using var scope = serviceProvider.CreateAsyncScope();
            await ProcessEntryAsync(scope.ServiceProvider, entry.EntryId, cancellationToken);
        }
    }

    private static async Task ProcessEntryAsync(IServiceProvider serviceProvider, string entryId, CancellationToken cancellationToken)
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
        var entry = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId).FirstOrDefaultAsync(cancellationToken);

        if (entry == null || entry.Status.ShouldStopImport())
        {
            return;
        }

        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(entry.ContentType);

        if (contentTypeDefinition == null)
        {
            await SaveEntryWithErrorAsync(session, clock, entry, localizer["The content definition was removed."], cancellationToken);
            return;
        }

        var fileInfo = await fileStore.GetFileInfoAsync(entry.StoredFileName);

        if (fileInfo == null || fileInfo.Length == 0)
        {
            await SaveEntryWithErrorAsync(session, clock, entry, localizer["The import file no longer exists."], cancellationToken);
            return;
        }

        var batchSize = contentImportOptions.ImportBatchSize < 1
            ? ContentImportOptions.DefaultImportBatchSize
            : contentImportOptions.ImportBatchSize;

        var progressPart = entry.GetOrCreate<ImportFileProcessStatsPart>();
        progressPart.Errors ??= [];
        progressPart.ErrorMessages ??= [];

        var isResuming = progressPart.CurrentRow > 0;

        entry.Status = ContentTransferEntryStatus.Processing;
        entry.Error = null;
        entry.CompletedUtc = null;

        if (!isResuming)
        {
            progressPart.CurrentRow = 0;
            progressPart.TotalProcessed = 0;
            progressPart.ImportedCount = 0;
            progressPart.Errors.Clear();
            progressPart.ErrorMessages.Clear();
        }

        entry.Put(progressPart);

        await session.SaveAsync(entry, false, collection: null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        await using var fileStream = await fileStore.GetFileStreamAsync(fileInfo);

        try
        {
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

            var completed = await ProcessFileInBatchesAsync(
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
                cancellationToken);

            if (!completed)
            {
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is stopping. The entry stays in Processing and the next run resumes it from its saved row.
            return;
        }
        catch (Exception ex)
        {
            // The session that ran the import is unusable after a failed save, and saving the failure through it
            // fails too, which left the entry in Processing with no error and the same rows retried every run.
            await RecordImportFailureAsync(serviceProvider, entry.EntryId, ex, localizer, cancellationToken);
            return;
        }

        var nowUtc = clock.UtcNow;
        entry.ProcessSaveUtc = nowUtc;
        entry.CompletedUtc = nowUtc;
        entry.Status = progressPart.Errors.Count > 0
            ? ContentTransferEntryStatus.CompletedWithErrors
            : ContentTransferEntryStatus.Completed;
        entry.Put(progressPart);

        await session.SaveAsync(entry, false, collection: null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static async Task<bool> ProcessFileInBatchesAsync(
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
        CancellationToken cancellationToken)
    {
        using var reader = formatProvider.CreateReader(stream);

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

        var rowIndex = 1;
        var hasFilters = activeRowFilters.Count > 0;
        var importInterrupted = false;

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

            progressPart.TotalProcessed++;
            progressPart.CurrentRow = rowIndex;

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
                var shouldSkip = false;
                string filterSkipReason = null;

                if (hasFilters)
                {
                    var filterContext = new ContentImportRowFilterContext()
                    {
                        Row = dataRow,
                        Columns = dataTable.Columns,
                        ContentTypeDefinition = contentTypeDefinition,
                        Entry = entry,
                        RowIndex = rowIndex,
                    };

                    foreach (var filter in activeRowFilters)
                    {
                        if (await filter.ShouldSkipRowAsync(filterContext))
                        {
                            shouldSkip = true;
                            filterSkipReason = filterContext.SkipReason;
                            break;
                        }
                    }
                }

                if (shouldSkip)
                {
                    if (!string.IsNullOrWhiteSpace(filterSkipReason))
                    {
                        AddRowError(progressPart, rowIndex, filterSkipReason);
                    }

                    rowIndex++;
                    continue;
                }

                var contentItemId = GetImportContentItemId(dataRow, indexOfKeyColumn);

                if (!string.IsNullOrEmpty(contentItemId))
                {
                    existingRows[contentItemId] = new KeyValuePair<int, DataRow>(rowIndex, dataRow);
                }
                else
                {
                    newRecords[rowIndex] = dataRow;
                }
            }

            if (newRecords.Count + existingRows.Count >= batchSize)
            {
                var batchProcessed = await ProcessBatchAsync(
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
            return false;
        }

        if (newRecords.Count + existingRows.Count > 0)
        {
            return await ProcessBatchAsync(
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
                cancellationToken);
        }

        return true;
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
                    isNew);
            }
        }

        foreach (var record in newRecords)
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
                true);
        }

        entry.ProcessSaveUtc = clock.UtcNow;
        entry.Put(progressPart);

        await session.SaveAsync(entry, false, collection: null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        return true;
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
                AddRowError(progressPart, rowIndex, FormatValidationErrors(validationResult));
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

            progressPart.Errors.Remove(rowIndex);
            progressPart.ErrorMessages.Remove(rowIndex);
        }
        catch (Exception ex)
        {
            AddRowError(progressPart, rowIndex, ex.Message);
        }
    }

    private static void AddRowError(ImportFileProcessStatsPart progressPart, int rowIndex, string errorMessage)
    {
        progressPart.Errors ??= [];
        progressPart.ErrorMessages ??= [];

        progressPart.Errors.Add(rowIndex);
        progressPart.ErrorMessages[rowIndex] = string.IsNullOrWhiteSpace(errorMessage)
            ? "The row failed to import."
            : errorMessage;
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
    private static async Task RecordImportFailureAsync(
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
                "The import of entry '{EntryId}' stopped because the database did not respond in time. It will resume from its last saved row on the next run.",
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
                return;
            }

            if (isTransient)
            {
                entry.Error = localizer["The database did not respond in time. The import will resume automatically: {0}", exception.Message];
                entry.ProcessSaveUtc = clock.UtcNow;

                await session.SaveAsync(entry, false, collection: null, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                return;
            }

            await SaveEntryWithErrorAsync(session, clock, entry, localizer["Error processing file: {0}", exception.Message], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The database may still be unavailable. The entry then stays in Processing and the next run resumes it.
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Could not record why the import of entry '{EntryId}' stopped.", entryId);
            }
        }
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
