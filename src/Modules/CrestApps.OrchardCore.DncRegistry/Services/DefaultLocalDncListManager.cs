using CrestApps.OrchardCore.DncRegistry.BackgroundTasks;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Default implementation of <see cref="ILocalDncListManager"/> that stores
/// phone numbers in YesSql and supports CSV import with phone number normalization.
/// </summary>
internal sealed class DefaultLocalDncListManager : ILocalDncListManager
{
    private const int BatchSize = 100;
    private const int DeleteBatchSize = 500;

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
                initSession.Save(list, collection: DncRegistryConstants.CollectionName);
                await initSession.SaveChangesAsync(cancellationToken);

                return;
            }

            var isResuming = list.TotalProcessed > 0
                && (list.Status == LocalDncListStatus.Paused || list.Status == LocalDncListStatus.Processing);

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

            initSession.Save(list, collection: DncRegistryConstants.CollectionName);
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

            var failSession = store.CreateSession();

            try
            {
                var trackedList = await failSession.Query<LocalDncList, LocalDncListIndex>(
                    i => i.ListId == list.ListId, collection: DncRegistryConstants.CollectionName)
                    .FirstOrDefaultAsync(cancellationToken);

                if (trackedList != null)
                {
                    trackedList.Status = list.Status;
                    trackedList.Error = list.Error;
                    trackedList.ProcessSaveUtc = list.ProcessSaveUtc;
                    trackedList.CompletedUtc = list.CompletedUtc;

                    failSession.Save(trackedList, collection: DncRegistryConstants.CollectionName);
                }

                await failSession.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                await failSession.DisposeAsync();
            }

            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to process local DNC list '{ListId}'.",
                    list.ListId);
            }

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

            if (trackedList != null)
            {
                trackedList.PhoneNumberCount = list.ImportedCount;
                trackedList.Status = LocalDncListStatus.Completed;
                trackedList.Error = null;
                trackedList.ProcessSaveUtc = _clock.UtcNow;
                trackedList.CompletedUtc = _clock.UtcNow;
                trackedList.TotalProcessed = list.TotalProcessed;
                trackedList.ImportedCount = list.ImportedCount;
                trackedList.ErrorMessages = list.ErrorMessages;

                completeSession.Save(trackedList, collection: DncRegistryConstants.CollectionName);
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

        _session.Save(list, collection: DncRegistryConstants.CollectionName);
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

        _session.Save(list, collection: DncRegistryConstants.CollectionName);
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

        _session.Save(list, collection: DncRegistryConstants.CollectionName);
        await _session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string listId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            LocalDncImportBackgroundTask.GetImportLockKey(listId),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMinutes(5));

        if (!locked)
        {
            throw new InvalidOperationException($"The local DNC list '{listId}' is currently being processed.");
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

        // Delete entries in batches to avoid excessive memory consumption and timeouts.
        while (true)
        {
            var batchSession = store.CreateSession();

            try
            {
                var entries = (await batchSession.Query<LocalDncEntry, LocalDncEntryIndex>(
                    i => i.ListId == listId, collection: DncRegistryConstants.CollectionName)
                    .Take(DeleteBatchSize)
                    .ListAsync(cancellationToken))
                    .ToList();

                if (entries.Count == 0)
                {
                    break;
                }

                foreach (var entry in entries)
                {
                    batchSession.Delete(entry, collection: DncRegistryConstants.CollectionName);
                }

                await batchSession.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                await batchSession.DisposeAsync();
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Deleted local DNC list '{Name}' ({ListId}) with all its entries.",
                list.Name,
                list.ListId);
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
            var store = _session.Store;
            var lookupSession = store.CreateSession();

            try
            {
                var existingEntries = await lookupSession.Query<LocalDncEntry, LocalDncEntryIndex>(
                    i => i.ListId == list.ListId, collection: DncRegistryConstants.CollectionName)
                    .ListAsync(cancellationToken);

                foreach (var existing in existingEntries)
                {
                    seenNumbers.Add(existing.PhoneNumber);
                }
            }
            finally
            {
                await lookupSession.DisposeAsync();
            }
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
                AddRowError(list, rowIndex, "Header row ignored.");
                continue;
            }

            if (!_phoneNumberService.TryFormatToE164(value, list.CountryCode, out var e164Number))
            {
                AddRowError(list, rowIndex, "Row ignored because it does not contain a valid phone number.");
                continue;
            }

            if (!seenNumbers.Add(e164Number))
            {
                AddRowError(list, rowIndex, "Duplicate phone number ignored.");
                continue;
            }

            batchEntries.Add(new LocalDncEntry
            {
                EntryId = IdGenerator.GenerateId(),
                ListId = list.ListId,
                CountryCode = list.CountryCode,
                PhoneNumber = e164Number,
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

                batchSession.Save(trackedList, collection: DncRegistryConstants.CollectionName);
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

            batchSession.Save(trackedList, collection: DncRegistryConstants.CollectionName);

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

    private static void AddRowError(LocalDncList list, int rowIndex, string errorMessage)
    {
        list.ErrorMessages ??= [];
        list.ErrorMessages[rowIndex] = string.IsNullOrWhiteSpace(errorMessage)
            ? "The row was ignored."
            : errorMessage;
    }

}
