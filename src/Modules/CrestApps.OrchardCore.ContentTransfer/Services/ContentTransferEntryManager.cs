using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

internal sealed class ContentTransferEntryManager : IContentTransferEntryManager
{
    private static readonly TimeSpan _deleteLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _deleteLockExpiration = TimeSpan.FromMinutes(30);

    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IContentTransferFileStore _contentTransferFileStore;
    private readonly IClock _clock;

    public ContentTransferEntryManager(
        ISession session,
        IDistributedLock distributedLock,
        IContentTransferFileStore contentTransferFileStore,
        IClock clock)
    {
        _session = session;
        _distributedLock = distributedLock;
        _contentTransferFileStore = contentTransferFileStore;
        _clock = clock;
    }

    public async Task PauseImportAsync(string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
                x.EntryId == entryId
                && x.Direction == ContentTransferDirection.Import)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry == null || entry.Status != ContentTransferEntryStatus.Processing)
        {
            return;
        }

        entry.Status = ContentTransferEntryStatus.Paused;
        entry.Error = null;
        entry.ProcessSaveUtc = _clock.UtcNow;

        _session.Save(entry);
        await _session.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeImportAsync(string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
                x.EntryId == entryId
                && x.Direction == ContentTransferDirection.Import)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry == null || entry.Status == ContentTransferEntryStatus.Deleting)
        {
            return;
        }

        entry.Status = ContentTransferEntryStatus.Processing;
        entry.Error = null;
        entry.CompletedUtc = null;
        entry.ProcessSaveUtc = _clock.UtcNow;

        _session.Save(entry);
        await _session.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsDeletingAsync(string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry == null)
        {
            return;
        }

        entry.Status = ContentTransferEntryStatus.Deleting;
        entry.ProcessSaveUtc = _clock.UtcNow;

        _session.Save(entry);
        await _session.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        var store = _session.Store;
        ContentTransferEntry entry;

        await using (var lookupSession = store.CreateSession())
        {
            entry = await lookupSession.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (entry == null)
        {
            return;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetProcessingLockKey(entry),
            _deleteLockTimeout,
            _deleteLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The content transfer entry '{entryId}' is currently being processed.");
        }

        await using var acquiredLock = locker;

        if (!string.IsNullOrWhiteSpace(entry.StoredFileName))
        {
            await _contentTransferFileStore.TryDeleteFileAsync(entry.StoredFileName);
        }

        await using var deleteSession = store.CreateSession();
        var trackedEntry = await deleteSession.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (trackedEntry == null)
        {
            return;
        }

        deleteSession.Delete(trackedEntry);
        await deleteSession.SaveChangesAsync(cancellationToken);
    }

    private static string GetProcessingLockKey(ContentTransferEntry entry)
        => entry.Direction == ContentTransferDirection.Import
            ? ImportFilesBackgroundTask.GetImportLockKey(entry.EntryId)
            : ExportFilesBackgroundTask.GetExportLockKey(entry.EntryId);
}
