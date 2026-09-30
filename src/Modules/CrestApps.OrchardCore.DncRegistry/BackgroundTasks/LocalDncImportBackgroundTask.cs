using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.DncRegistry.BackgroundTasks;

[BackgroundTask(
    Title = "Local DNC Import Processor",
    Schedule = "*/10 * * * *",
    Description = "Regularly processes pending Local DNC imports and resumes imports and deletions that stalled or failed.",
    LockTimeout = 3_000,
    LockExpiration = 30_000)]
public sealed class LocalDncImportBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _importLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _importLockExpiration = TimeSpan.FromMinutes(30);

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => ProcessEntriesAsync(serviceProvider, cancellationToken: cancellationToken);

    internal static async Task ProcessEntriesAsync(
        IServiceProvider serviceProvider,
        string listId = null,
        CancellationToken cancellationToken = default)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var distributedLock = serviceProvider.GetRequiredService<IDistributedLock>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<LocalDncImportBackgroundTask>>();

        List<(LocalDncList List, LocalDncListRecoveryAction Action)> work;

        if (string.IsNullOrEmpty(listId))
        {
            var lists = await session.Query<LocalDncList, LocalDncListIndex>(x =>
                    x.Status == LocalDncListStatus.Pending
                    || x.Status == LocalDncListStatus.Processing
                    || x.Status == LocalDncListStatus.Failed
                    || x.Status == LocalDncListStatus.Deleting,
                    collection: DncRegistryConstants.CollectionName)
                .OrderBy(x => x.CreatedUtc)
                .ListAsync(cancellationToken);

            var utcNow = clock.UtcNow;

            // Deletions go first: they free the space an import might be failing for.
            work = lists
                .Select(list => (List: list, Action: LocalDncListRecoveryPolicy.Evaluate(list, utcNow)))
                .Where(item => item.Action != LocalDncListRecoveryAction.None)
                .OrderBy(item => item.Action == LocalDncListRecoveryAction.Delete ? 0 : 1)
                .ToList();
        }
        else
        {
            var lists = await session.Query<LocalDncList, LocalDncListIndex>(x =>
                    x.ListId == listId
                    && x.Status != LocalDncListStatus.Completed
                    && x.Status != LocalDncListStatus.Deleting,
                    collection: DncRegistryConstants.CollectionName)
                .OrderBy(x => x.CreatedUtc)
                .ListAsync(cancellationToken);

            work = lists
                .Select(list => (List: list, Action: LocalDncListRecoveryAction.Import))
                .ToList();
        }

        foreach (var (list, action) in work)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (action == LocalDncListRecoveryAction.Delete)
                {
                    await ResumeDeletionAsync(serviceProvider, list, logger, cancellationToken);

                    continue;
                }

                await ImportAsync(serviceProvider, distributedLock, list, logger, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // One list must not keep the others waiting; it is picked up again on the next run.
                logger.LogError(
                    ex,
                    "The local DNC task could not {Action} list '{ListId}' ({Status}). It will be retried on the next run.",
                    action == LocalDncListRecoveryAction.Delete ? "delete" : "import",
                    list.ListId,
                    list.Status);
            }
        }
    }

    internal static string GetImportLockKey(string listId)
        => "local-dnc-import:" + listId;

    private static async Task ResumeDeletionAsync(
        IServiceProvider serviceProvider,
        LocalDncList list,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Resuming the deletion of local DNC list '{ListId}' ('{Name}'), which has been deleting without progress since {ProcessSaveUtc:o}. Last error: {Error}",
                list.ListId,
                list.Name,
                list.ProcessSaveUtc,
                list.Error ?? "(none)");
        }

        // DeleteAsync takes the list lock itself, so it must not be held here.
        await using var scope = serviceProvider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<ILocalDncListManager>();
        await manager.DeleteAsync(list.ListId, cancellationToken);
    }

    private static async Task ImportAsync(
        IServiceProvider serviceProvider,
        IDistributedLock distributedLock,
        LocalDncList list,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        (var locker, var locked) = await distributedLock.TryAcquireLockAsync(
            GetImportLockKey(list.ListId),
            _importLockTimeout,
            _importLockExpiration);

        if (!locked)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Skipped local DNC list '{ListId}' ({Status}) because another worker holds its lock.",
                    list.ListId,
                    list.Status);
            }

            return;
        }

        await using var acquiredLock = locker;

        if (list.Status == LocalDncListStatus.Processing && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Resuming the import of local DNC list '{ListId}' ('{Name}') at row {TotalProcessed} of {TotalRecords}; it has saved no progress since {ProcessSaveUtc:o}.",
                list.ListId,
                list.Name,
                list.TotalProcessed,
                list.TotalRecords,
                list.ProcessSaveUtc);
        }
        else if (list.Status == LocalDncListStatus.Failed && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Retrying the failed import of local DNC list '{ListId}' ('{Name}') at row {TotalProcessed} of {TotalRecords} (automatic attempt {Attempt} of {MaxAttempts}). Last error: {Error}",
                list.ListId,
                list.Name,
                list.TotalProcessed,
                list.TotalRecords,
                list.FailedAttempts + 1,
                LocalDncListRecoveryPolicy.MaxAutomaticImportAttempts,
                list.Error);
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<ILocalDncListManager>();
        await manager.ProcessImportAsync(list.ListId, cancellationToken);
    }
}
