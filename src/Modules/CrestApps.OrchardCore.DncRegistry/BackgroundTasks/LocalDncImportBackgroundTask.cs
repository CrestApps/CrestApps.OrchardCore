using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.DncRegistry.BackgroundTasks;

[BackgroundTask(
    Title = "Local DNC Import Processor",
    Schedule = "*/10 * * * *",
    Description = "Regularly checks for pending Local DNC imports and processes them.",
    LockTimeout = 3_000,
    LockExpiration = 30_000)]
public sealed class LocalDncImportBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _importLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _importLockExpiration = TimeSpan.FromMinutes(30);

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => ProcessEntriesAsync(serviceProvider, cancellationToken);

    internal static async Task ProcessEntriesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        string listId = null)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var distributedLock = serviceProvider.GetRequiredService<IDistributedLock>();

        IEnumerable<LocalDncList> lists;

        if (string.IsNullOrEmpty(listId))
        {
            lists = await session.Query<LocalDncList, LocalDncListIndex>(x =>
                    x.Status == LocalDncListStatus.Pending || x.Status == LocalDncListStatus.Processing,
                    collection: DncRegistryConstants.CollectionName)
                .OrderBy(x => x.CreatedUtc)
                .ListAsync(cancellationToken);
        }
        else
        {
            lists = await session.Query<LocalDncList, LocalDncListIndex>(x =>
                    x.ListId == listId
                    && x.Status != LocalDncListStatus.Completed
                    && x.Status != LocalDncListStatus.Deleting,
                    collection: DncRegistryConstants.CollectionName)
                .OrderBy(x => x.CreatedUtc)
                .ListAsync(cancellationToken);
        }

        foreach (var list in lists)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            (var locker, var locked) = await distributedLock.TryAcquireLockAsync(
                GetImportLockKey(list.ListId),
                _importLockTimeout,
                _importLockExpiration);

            if (!locked)
            {
                continue;
            }

            await using var acquiredLock = locker;
            await using var scope = serviceProvider.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<ILocalDncListManager>();
            await manager.ProcessImportAsync(list.ListId, cancellationToken);
        }
    }

    internal static string GetImportLockKey(string listId)
        => "local-dnc-import:" + listId;
}
