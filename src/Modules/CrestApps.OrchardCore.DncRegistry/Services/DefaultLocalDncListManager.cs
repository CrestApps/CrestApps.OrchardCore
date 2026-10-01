using CrestApps.OrchardCore.DncRegistry.BackgroundTasks;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Dapper;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Default implementation of <see cref="ILocalDncListManager"/> that stores
/// phone numbers in YesSql and supports CSV import with phone number normalization.
/// </summary>
internal sealed class DefaultLocalDncListManager : ILocalDncListManager
{
    private const int BatchSize = 100;
    private const int DeleteBatchSize = 1_000;
    private const int DeleteCommandTimeoutSeconds = 120;
    private const int ExistingNumbersPageSize = 5_000;
    private const int DeleteHeartbeatInterval = 20;

    private static readonly TimeSpan _deleteLockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _deleteLockExpiration = TimeSpan.FromMinutes(30);

    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly ILocalDncFileStore _fileStore;
    private readonly IClock _clock;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultLocalDncListManager"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="distributedLock">The distributed lock service.</param>
    /// <param name="fileStore">The tenant-local file store.</param>
    /// <param name="clock">The clock service.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="logger">The logger.</param>
    public DefaultLocalDncListManager(
        ISession session,
        IDistributedLock distributedLock,
        ILocalDncFileStore fileStore,
        IClock clock,
        IPhoneNumberService phoneNumberService,
        ILogger<DefaultLocalDncListManager> logger)
    {
        _session = session;
        _distributedLock = distributedLock;
        _fileStore = fileStore;
        _clock = clock;
        _phoneNumberService = phoneNumberService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LocalDncList> QueueImportAsync(
        string name,
        string countryCode,
        string uploadedFileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedFileName);
        ArgumentNullException.ThrowIfNull(fileStream);

        var listId = IdGenerator.GenerateId();
        var extension = Path.GetExtension(uploadedFileName);

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".csv";
        }

        var storedFileName = await _fileStore.CreateFileFromStreamAsync(
            listId + extension,
            fileStream,
            overwrite: false);

        var list = new LocalDncList
        {
            ListId = listId,
            CountryCode = countryCode.ToUpperInvariant(),
            Name = name.Trim(),
            UploadedFileName = uploadedFileName,
            StoredFileName = storedFileName,
            PhoneNumberCount = 0,
            TotalRecords = 0,
            TotalProcessed = 0,
            ImportedCount = 0,
            ErrorMessages = [],
            Status = LocalDncListStatus.Pending,
            Error = null,
            CreatedUtc = _clock.UtcNow,
            ProcessSaveUtc = null,
            CompletedUtc = null,
        };

        await _session.SaveAsync(list, false, DncRegistryConstants.CollectionName, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Queued local DNC list {ListId} named '{Name}' ({CountryCode}) from uploaded file '{UploadedFileName}'.",
                list.ListId,
                list.Name,
                list.CountryCode,
                list.UploadedFileName);
        }

        return list;
    }

    public async Task<LocalDncList> FindByIdAsync(string listId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);

        return await _session.Query<LocalDncList, LocalDncListIndex>(
            i => i.ListId == listId, collection: DncRegistryConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ProcessImportAsync(
        string listId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);

        var store = _session.Store;

        // Phase 1: Load list, validate, and set initial Processing state.
        LocalDncList list;

        var initSession = store.CreateSession();

        try
        {
            list = await initSession.Query<LocalDncList, LocalDncListIndex>(
                i => i.ListId == listId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken);

            if (list == null || list.Status == LocalDncListStatus.Completed || list.Status == LocalDncListStatus.Deleting)
            {
                return;
            }

            var fileInfo = await _fileStore.GetFileInfoAsync(list.StoredFileName);

            if (fileInfo == null || fileInfo.Length == 0)
            {
                SaveListFailure(list, "The uploaded DNC file no longer exists.");

                // Retrying cannot bring the file back, so the background task leaves this list alone.
                list.FailedAttempts = LocalDncListRecoveryPolicy.MaxAutomaticImportAttempts;
                await initSession.SaveAsync(list, false, DncRegistryConstants.CollectionName, cancellationToken);
                await initSession.SaveChangesAsync(cancellationToken);

                return;
            }

            // Entries and TotalProcessed are saved in the same transaction, so a list that already made
            // progress continues from its saved row. A failed list used to restart from row one without
            // removing what it had imported, which would duplicate every number already saved.
            var isResuming = list.TotalProcessed > 0
                && list.Status is LocalDncListStatus.Paused or LocalDncListStatus.Processing or LocalDncListStatus.Failed;

            if (isResuming && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Resuming local DNC list {ListId} from {Status} at row {TotalProcessed}.",
                    list.ListId,
                    list.Status,
                    list.TotalProcessed);
            }

            list.Status = LocalDncListStatus.Processing;
            list.Error = null;
            list.CompletedUtc = null;
            list.ProcessSaveUtc = _clock.UtcNow;

            if (!isResuming)
            {
                list.PhoneNumberCount = 0;
                list.TotalProcessed = 0;
                list.ImportedCount = 0;
                list.ErrorMessages ??= [];
                list.ErrorMessages.Clear();
            }

            await using (var countStream = await _fileStore.GetFileStreamAsync(fileInfo))
            {
                list.TotalRecords = await CountTotalRecordsAsync(countStream, cancellationToken);
            }

            await initSession.SaveAsync(list, false, DncRegistryConstants.CollectionName, cancellationToken);
            await initSession.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await initSession.DisposeAsync();
        }

        // Phase 2: Process the CSV file in batches.
        var skipRows = list.TotalProcessed;

        try
        {
            var fileInfo2 = await _fileStore.GetFileInfoAsync(list.StoredFileName);

            await using (var stream = await _fileStore.GetFileStreamAsync(fileInfo2))
            {
                await ProcessCsvAsync(list, stream, skipRows, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            SaveListFailure(list, ex.Message);

            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to process local DNC list '{ListId}' at row {TotalProcessed} of {TotalRecords}.",
                    list.ListId,
                    list.TotalProcessed,
                    list.TotalRecords);
            }

            await TrySaveImportFailureAsync(list, cancellationToken);

            return;
        }

        // Phase 3: Mark as completed only if processing was not interrupted.
        if (list.Status != LocalDncListStatus.Processing)
        {
            // Processing was interrupted (paused or deleted externally). Don't mark as completed.
            return;
        }

        var completeSession = store.CreateSession();

        try
        {
            var trackedList = await completeSession.Query<LocalDncList, LocalDncListIndex>(
                i => i.ListId == list.ListId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken);

            // A delete or pause issued after the last batch wins over completing the import.
            if (trackedList != null && trackedList.Status == LocalDncListStatus.Processing)
            {
                trackedList.PhoneNumberCount = list.ImportedCount;
                trackedList.Status = LocalDncListStatus.Completed;
                trackedList.FailedAttempts = 0;
                trackedList.Error = null;
                trackedList.ProcessSaveUtc = _clock.UtcNow;
                trackedList.CompletedUtc = _clock.UtcNow;
                trackedList.TotalProcessed = list.TotalProcessed;
                trackedList.ImportedCount = list.ImportedCount;
                trackedList.ErrorMessages = list.ErrorMessages;

                await completeSession.SaveAsync(trackedList, false, DncRegistryConstants.CollectionName, cancellationToken);
            }

            await completeSession.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await completeSession.DisposeAsync();
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Imported local DNC list '{Name}' ({CountryCode}) with {Count} phone numbers and {ErrorCount} row errors.",
                list.Name,
                list.CountryCode,
                list.ImportedCount,
                list.ErrorMessages?.Count ?? 0);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LocalDncList>> GetListsAsync(
        string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var upperCountry = countryCode.ToUpperInvariant();

            return await _session.Query<LocalDncList, LocalDncListIndex>(
                i => i.CountryCode == upperCountry, collection: DncRegistryConstants.CollectionName)
                .ListAsync(cancellationToken);
        }

        return await _session.Query<LocalDncList, LocalDncListIndex>(collection: DncRegistryConstants.CollectionName)
            .ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> GetCountAsync(
        string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var upperCountry = countryCode.ToUpperInvariant();

            return await _session.Query<LocalDncList, LocalDncListIndex>(
                i => i.CountryCode == upperCountry, collection: DncRegistryConstants.CollectionName)
                .CountAsync(cancellationToken);
        }

        return await _session.Query<LocalDncList, LocalDncListIndex>(collection: DncRegistryConstants.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LocalDncList>> GetListsAsync(
        int page,
        int pageSize,
        string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var upperCountry = countryCode.ToUpperInvariant();

            return await _session.Query<LocalDncList, LocalDncListIndex>(
                i => i.CountryCode == upperCountry, collection: DncRegistryConstants.CollectionName)
                .OrderByDescending(i => i.CreatedUtc)
                .Skip(skip)
                .Take(pageSize)
                .ListAsync(cancellationToken);
        }

        return await _session.Query<LocalDncList, LocalDncListIndex>(collection: DncRegistryConstants.CollectionName)
            .OrderByDescending(i => i.CreatedUtc)
            .Skip(skip)
            .Take(pageSize)
            .ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PauseImportAsync(
        string listId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);

        var list = await FindByIdAsync(listId, cancellationToken);

        if (list == null)
        {
            return;
        }

        if (list.Status != LocalDncListStatus.Processing)
        {
            return;
        }

        list.Status = LocalDncListStatus.Paused;
        list.Error = "Import was paused by the user.";
        list.ProcessSaveUtc = _clock.UtcNow;

        await _session.SaveAsync(list, false, DncRegistryConstants.CollectionName, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ResumeImportAsync(
        string listId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);

        var list = await FindByIdAsync(listId, cancellationToken);

        if (list == null)
        {
            return;
        }

        list.Status = LocalDncListStatus.Processing;
        list.Error = null;
        list.ProcessSaveUtc = _clock.UtcNow;

        // A manual resume gives the background task a fresh set of automatic retries.
        list.FailedAttempts = 0;

        await _session.SaveAsync(list, false, DncRegistryConstants.CollectionName, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MarkAsDeletingAsync(
        string listId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);

        var list = await FindByIdAsync(listId, cancellationToken);

        if (list == null)
        {
            return;
        }

        list.Status = LocalDncListStatus.Deleting;
        list.ProcessSaveUtc = _clock.UtcNow;

        // The old import error no longer applies, and it read as the reason the deletion was stuck.
        list.Error = null;

        await _session.SaveAsync(list, false, DncRegistryConstants.CollectionName, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string listId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);

        // A running import stops at its next batch once the list is marked as deleting, so wait long enough
        // for it to let go. This used to give up after one second and throw from the after-request job,
        // which left the list in Deleting with nothing to ever finish it. Now the list stays in Deleting
        // and the background task resumes the deletion.
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            LocalDncImportBackgroundTask.GetImportLockKey(listId),
            _deleteLockTimeout,
            _deleteLockExpiration);

        if (!locked)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Could not delete local DNC list '{ListId}' yet because another worker holds its lock. The background task will resume the deletion.",
                    listId);
            }

            return;
        }

        await using var acquiredLock = locker;

        var store = _session.Store;

        // Verify the list exists.
        LocalDncList list;
        var lookupSession = store.CreateSession();

        try
        {
            list = await lookupSession.Query<LocalDncList, LocalDncListIndex>(
                i => i.ListId == listId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken);
        }
        finally
        {
            await lookupSession.DisposeAsync();
        }

        if (list == null)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Deleting local DNC list '{Name}' ({ListId}) with up to {PhoneNumberCount} entries.",
                list.Name,
                list.ListId,
                list.PhoneNumberCount);
        }

        var deletedCount = 0;
        var batchCount = 0;

        try
        {
            var statements = LocalDncEntryDeleteStatements.Create(store.Configuration);
            var afterDocumentId = 0L;

            // Entries are deleted by the database in ranges of document ids rather than loaded and deleted one
            // at a time. Loading each entry, then sending one DELETE per document and one per index row, is
            // what made deleting a list of millions of numbers run for hours and time out on a small database.
            while (true)
            {
                var deleted = await DeleteNextEntryBatchAsync(store, statements, listId, afterDocumentId, cancellationToken);

                if (deleted.Count == 0)
                {
                    break;
                }

                afterDocumentId = deleted.LastDocumentId;
                deletedCount += deleted.Count;

                // The heartbeat keeps the background task from starting a second deletion of this list
                // while a long one is still running.
                if (++batchCount % DeleteHeartbeatInterval == 0)
                {
                    await TrySaveDeleteHeartbeatAsync(listId, error: null, cancellationToken);

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(
                            "Deleted {DeletedCount} entries of local DNC list '{ListId}' so far.",
                            deletedCount,
                            listId);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(list.StoredFileName))
            {
                await _fileStore.TryDeleteFileAsync(list.StoredFileName);
            }

            // Delete the list document itself.
            var deleteSession = store.CreateSession();

            try
            {
                var trackedList = await deleteSession.Query<LocalDncList, LocalDncListIndex>(
                    i => i.ListId == listId, collection: DncRegistryConstants.CollectionName)
                    .FirstOrDefaultAsync(cancellationToken);

                if (trackedList != null)
                {
                    deleteSession.Delete(trackedList, collection: DncRegistryConstants.CollectionName);
                    await deleteSession.SaveChangesAsync(cancellationToken);
                }
            }
            finally
            {
                await deleteSession.DisposeAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Deleting local DNC list '{ListId}' stopped after {DeletedCount} entries. The background task will resume it.",
                    listId,
                    deletedCount);
            }

            // Leave the list in Deleting and say why, so the next run picks it up again.
            await TrySaveDeleteHeartbeatAsync(
                listId,
                $"Deletion stopped and will resume automatically: {ex.Message}",
                cancellationToken);

            throw;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Deleted local DNC list '{Name}' ({ListId}) with all its {DeletedCount} entries.",
                list.Name,
                list.ListId,
                deletedCount);
        }
    }

    /// <summary>
    /// Deletes the next batch of a list's entries, documents and index rows together, in one transaction.
    /// The batch is the next <see cref="DeleteBatchSize"/> document ids of the list after
    /// <paramref name="afterDocumentId"/>, so each batch starts where the last one ended instead of searching
    /// the table again from the beginning.
    /// </summary>
    private static async Task<EntryDeleteBatch> DeleteNextEntryBatchAsync(
        IStore store,
        LocalDncEntryDeleteStatements statements,
        string listId,
        long afterDocumentId,
        CancellationToken cancellationToken)
    {
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(store.Configuration.IsolationLevel, cancellationToken);

        var documentIds = (await connection.QueryAsync<long>(new CommandDefinition(
            statements.SelectBatch,
            new { ListId = listId, After = afterDocumentId },
            transaction,
            DeleteCommandTimeoutSeconds,
            cancellationToken: cancellationToken)))
            .ToList();

        if (documentIds.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);

            return new EntryDeleteBatch(0, afterDocumentId);
        }

        var parameters = new
        {
            ListId = listId,
            After = afterDocumentId,
            UpTo = documentIds.Max(),
        };

        // The documents go first: their statement finds them through the index rows the second one removes.
        await connection.ExecuteAsync(new CommandDefinition(
            statements.DeleteDocuments,
            parameters,
            transaction,
            DeleteCommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            statements.DeleteIndexRows,
            parameters,
            transaction,
            DeleteCommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        return new EntryDeleteBatch(documentIds.Count, parameters.UpTo);
    }

    private readonly record struct EntryDeleteBatch(int Count, long LastDocumentId);

    /// <summary>
    /// The statements that delete a list's entries in ranges of document ids. Each one filters on the list and
    /// a document id range, which the existing (DocumentId, ListId, ...) index of the entry table covers, so no
    /// statement reads the whole table. Every value is a scalar parameter, so the statements have the same
    /// shape on every supported database.
    /// </summary>
    private sealed class LocalDncEntryDeleteStatements
    {
        private LocalDncEntryDeleteStatements(string selectBatch, string deleteDocuments, string deleteIndexRows)
        {
            SelectBatch = selectBatch;
            DeleteDocuments = deleteDocuments;
            DeleteIndexRows = deleteIndexRows;
        }

        public string SelectBatch { get; }

        public string DeleteDocuments { get; }

        public string DeleteIndexRows { get; }

        public static LocalDncEntryDeleteStatements Create(YesSql.IConfiguration configuration)
        {
            var dialect = configuration.SqlDialect;
            var prefix = configuration.TablePrefix;
            var schema = configuration.Schema;
            var indexTableName = configuration.TableNameConvention.GetIndexTable(typeof(LocalDncEntryIndex), DncRegistryConstants.CollectionName);
            var documentTableName = configuration.TableNameConvention.GetDocumentTable(DncRegistryConstants.CollectionName);

            var indexTable = dialect.QuoteForTableName(prefix + indexTableName, schema);
            var documentTable = dialect.QuoteForTableName(prefix + documentTableName, schema);
            var documentIdColumn = dialect.QuoteForColumnName("DocumentId");
            var listIdColumn = dialect.QuoteForColumnName(nameof(LocalDncEntryIndex.ListId));
            var idColumn = dialect.QuoteForColumnName("Id");

            var select = new SqlBuilder(prefix, dialect);
            select.Select();
            select.Selector(documentIdColumn);
            select.Table(indexTableName, alias: null, schema);
            select.WhereAnd($"{listIdColumn} = @ListId");
            select.WhereAnd($"{documentIdColumn} > @After");
            select.OrderBy(documentIdColumn);
            select.Take(DeleteBatchSize.ToString(System.Globalization.CultureInfo.InvariantCulture));

            var range = $"{listIdColumn} = @ListId AND {documentIdColumn} > @After AND {documentIdColumn} <= @UpTo";

            return new LocalDncEntryDeleteStatements(
                select.ToSqlString(),
                $"DELETE FROM {documentTable} WHERE {idColumn} IN (SELECT {documentIdColumn} FROM {indexTable} WHERE {range})",
                $"DELETE FROM {indexTable} WHERE {range}");
        }
    }

    /// <summary>
    /// Records a failed import attempt without overwriting a status set while the import ran.
    /// This save can fail too (a full database refuses updates); the list then stays in Processing
    /// and the background task resumes it once its progress goes stale.
    /// </summary>
    private async Task TrySaveImportFailureAsync(LocalDncList list, CancellationToken cancellationToken)
    {
        var failSession = _session.Store.CreateSession();

        try
        {
            var trackedList = await failSession.Query<LocalDncList, LocalDncListIndex>(
                i => i.ListId == list.ListId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken);

            if (trackedList == null)
            {
                return;
            }

            if (trackedList.Status != LocalDncListStatus.Processing)
            {
                // A delete or pause arrived while the import ran. Marking the list as failed here would
                // silently cancel the user's delete.
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Kept local DNC list '{ListId}' in {Status} after its import failed.",
                        list.ListId,
                        trackedList.Status);
                }

                return;
            }

            trackedList.Status = list.Status;
            trackedList.Error = list.Error;
            trackedList.ProcessSaveUtc = list.ProcessSaveUtc;
            trackedList.CompletedUtc = list.CompletedUtc;
            trackedList.FailedAttempts++;

            await failSession.SaveAsync(trackedList, false, DncRegistryConstants.CollectionName, cancellationToken);
            await failSession.SaveChangesAsync(cancellationToken);

            if (trackedList.FailedAttempts >= LocalDncListRecoveryPolicy.MaxAutomaticImportAttempts
                && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Local DNC list '{ListId}' failed {FailedAttempts} times in a row and will not be retried automatically. Use 'Process now' to retry it.",
                    list.ListId,
                    trackedList.FailedAttempts);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Could not record the failure of local DNC list '{ListId}'. It stays in Processing and the background task will resume it.",
                    list.ListId);
            }
        }
        finally
        {
            await failSession.DisposeAsync();
        }
    }

    /// <summary>
    /// Saves deletion progress in its own session. A failure here is only logged: this runs while the
    /// database may be full, and freeing that space is the point of the deletion.
    /// </summary>
    private async Task TrySaveDeleteHeartbeatAsync(string listId, string error, CancellationToken cancellationToken)
    {
        var heartbeatSession = _session.Store.CreateSession();

        try
        {
            var trackedList = await heartbeatSession.Query<LocalDncList, LocalDncListIndex>(
                i => i.ListId == listId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken);

            if (trackedList == null)
            {
                return;
            }

            trackedList.Status = LocalDncListStatus.Deleting;
            trackedList.Error = error;
            trackedList.ProcessSaveUtc = _clock.UtcNow;

            await heartbeatSession.SaveAsync(trackedList, false, DncRegistryConstants.CollectionName, cancellationToken);
            await heartbeatSession.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    ex,
                    "Could not save the deletion progress of local DNC list '{ListId}'.",
                    listId);
            }
        }
        finally
        {
            await heartbeatSession.DisposeAsync();
        }
    }

    private void SaveListFailure(LocalDncList list, string error)
    {
        list.Status = LocalDncListStatus.Failed;
        list.Error = error;
        list.ProcessSaveUtc = _clock.UtcNow;
        list.CompletedUtc = _clock.UtcNow;
    }

    private static async Task<int> CountTotalRecordsAsync(
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        var totalRecords = 0;
        using var reader = new StreamReader(fileStream);

        while (await reader.ReadLineAsync(cancellationToken) is not null)
        {
            totalRecords++;
        }

        return totalRecords;
    }

    private async Task ProcessCsvAsync(
        LocalDncList list,
        Stream fileStream,
        int skipRows,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(fileStream);
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowIndex = 0;
        var batchEntries = new List<LocalDncEntry>(BatchSize);

        // When resuming, load already-imported phone numbers to prevent duplicates.
        if (skipRows > 0)
        {
            await LoadExistingNumbersAsync(list.ListId, seenNumbers, cancellationToken);
        }

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            rowIndex++;

            // Skip rows that were already processed in a previous run.
            if (rowIndex <= skipRows)
            {
                continue;
            }

            list.TotalProcessed++;

            if (string.IsNullOrWhiteSpace(line))
            {
                AddRowError(list, rowIndex, "Blank row ignored.");
                continue;
            }

            var nonEmptyFields = line.Split(',')
                .Select(field => field.Trim().Trim('"'))
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .ToArray();

            if (nonEmptyFields.Length == 0)
            {
                AddRowError(list, rowIndex, "Blank row ignored.");
                continue;
            }

            if (nonEmptyFields.Length > 1)
            {
                AddRowError(list, rowIndex, "Row ignored because the file must contain a single column with phone numbers only.");
                continue;
            }

            var value = nonEmptyFields[0];

            if (rowIndex == 1 && value.Any(char.IsLetter))
            {
                // A header is expected and optional, not a rejected record. Recording it as an error would
                // put it in the error download and mark a list whose every number imported as completed
                // with errors.
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Skipped the header row of local DNC list {ListId}; it is not counted as a rejected record.",
                        list.ListId);
                }

                continue;
            }

            if (!_phoneNumberService.TryParse(value, list.CountryCode, out var canonical))
            {
                AddRowError(list, rowIndex, "Row ignored because it does not contain a valid phone number.");
                continue;
            }

            if (!seenNumbers.Add(canonical.Value))
            {
                AddRowError(list, rowIndex, "Duplicate phone number ignored.");
                continue;
            }

            batchEntries.Add(new LocalDncEntry
            {
                EntryId = IdGenerator.GenerateId(),
                ListId = list.ListId,
                CountryCode = list.CountryCode,
                PhoneNumber = canonical.Value,
            });

            list.ImportedCount++;
            list.PhoneNumberCount = list.ImportedCount;

            if (batchEntries.Count >= BatchSize)
            {
                var shouldContinue = await FlushBatchAndUpdateProgressAsync(list, batchEntries, cancellationToken);

                if (!shouldContinue)
                {
                    return;
                }
            }
        }

        if (batchEntries.Count > 0)
        {
            await FlushBatchAndUpdateProgressAsync(list, batchEntries, cancellationToken);
        }
    }

    /// <summary>
    /// Flushes a batch of entries and updates the list progress.
    /// Returns <c>false</c> if processing should stop (e.g., the list was paused or is being deleted).
    /// </summary>
    private async Task<bool> FlushBatchAndUpdateProgressAsync(
        LocalDncList list,
        List<LocalDncEntry> entries,
        CancellationToken cancellationToken)
    {
        list.ProcessSaveUtc = _clock.UtcNow;

        var batchSession = _session.Store.CreateSession();

        try
        {
            // Fetch the list within this session to ensure we update the same document.
            var trackedList = await batchSession.Query<LocalDncList, LocalDncListIndex>(
                i => i.ListId == list.ListId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken);

            if (trackedList == null)
            {
                return false;
            }

            // Check if the status was changed externally (e.g., user paused or deleted).
            if (trackedList.Status == LocalDncListStatus.Paused || trackedList.Status == LocalDncListStatus.Deleting)
            {
                // Save entries that are already in this batch so we don't lose work.
                foreach (var entry in entries)
                {
                    await batchSession.SaveAsync(entry, false, DncRegistryConstants.CollectionName, cancellationToken);
                }

                // Update progress counters but preserve the externally-set status.
                trackedList.TotalProcessed = list.TotalProcessed;
                trackedList.ImportedCount = list.ImportedCount;
                trackedList.PhoneNumberCount = list.PhoneNumberCount;
                trackedList.ProcessSaveUtc = list.ProcessSaveUtc;
                trackedList.ErrorMessages = list.ErrorMessages;

                await batchSession.SaveAsync(trackedList, false, DncRegistryConstants.CollectionName, cancellationToken);
                await batchSession.SaveChangesAsync(cancellationToken);

                // Sync the in-memory status so callers see the change.
                list.Status = trackedList.Status;

                return false;
            }

            trackedList.TotalProcessed = list.TotalProcessed;
            trackedList.ImportedCount = list.ImportedCount;
            trackedList.PhoneNumberCount = list.PhoneNumberCount;
            trackedList.ProcessSaveUtc = list.ProcessSaveUtc;
            trackedList.ErrorMessages = list.ErrorMessages;
            trackedList.Status = list.Status;

            await batchSession.SaveAsync(trackedList, false, DncRegistryConstants.CollectionName, cancellationToken);

            foreach (var entry in entries)
            {
                await batchSession.SaveAsync(entry, false, DncRegistryConstants.CollectionName, cancellationToken);
            }

            await batchSession.SaveChangesAsync(cancellationToken);

            return true;
        }
        finally
        {
            entries.Clear();
            await batchSession.DisposeAsync();
        }
    }

    /// <summary>
    /// Reads the numbers a resumed import already saved, a page at a time from the index table. Loading
    /// every entry document in one query can time out on a large list, which made the resume itself fail.
    /// </summary>
    private async Task LoadExistingNumbersAsync(
        string listId,
        HashSet<string> seenNumbers,
        CancellationToken cancellationToken)
    {
        var lastId = 0L;

        while (true)
        {
            var lookupSession = _session.Store.CreateSession();

            try
            {
                var page = (await lookupSession.QueryIndex<LocalDncEntryIndex>(
                    i => i.ListId == listId && i.Id > lastId, collection: DncRegistryConstants.CollectionName)
                    .OrderBy(i => i.Id)
                    .Take(ExistingNumbersPageSize)
                    .ListAsync(cancellationToken))
                    .ToList();

                foreach (var existing in page)
                {
                    seenNumbers.Add(existing.PhoneNumber);
                }

                if (page.Count < ExistingNumbersPageSize)
                {
                    break;
                }

                lastId = page[^1].Id;
            }
            finally
            {
                await lookupSession.DisposeAsync();
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Loaded {Count} already-imported numbers for resumed local DNC list {ListId}.",
                seenNumbers.Count,
                listId);
        }
    }

    private static void AddRowError(LocalDncList list, int rowIndex, string errorMessage)
    {
        list.ErrorMessages ??= [];
        list.ErrorMessages[rowIndex] = string.IsNullOrWhiteSpace(errorMessage)
            ? "The row was ignored."
            : errorMessage;
    }

}
