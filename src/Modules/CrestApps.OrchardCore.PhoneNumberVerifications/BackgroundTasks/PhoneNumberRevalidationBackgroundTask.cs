using CrestApps.OrchardCore.PhoneNumberVerifications.Indexes;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.BackgroundTasks;

/// <summary>
/// Periodically revalidates contact phone numbers that have never been verified or whose
/// verification has expired. Work is processed in resilient batches so the task scales and
/// tolerates provider failures.
/// </summary>
[BackgroundTask(
    Title = "Phone Number Revalidation",
    Schedule = "0 3 * * *",
    Description = "Revalidates contact phone numbers that are due for verification.",
    LockTimeout = 3_000,
    LockExpiration = 60_000)]
public sealed class PhoneNumberRevalidationBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 50;
    private const string LockKey = "phone-number-revalidation";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(30);

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var distributedLock = serviceProvider.GetRequiredService<IDistributedLock>();

        (var locker, var locked) = await distributedLock.TryAcquireLockAsync(LockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return;
        }

        await using var acquiredLock = locker;

        var logger = serviceProvider.GetRequiredService<ILogger<PhoneNumberRevalidationBackgroundTask>>();

        // Track processed identifiers so content items that cannot be verified (for example, when no
        // phone number can be resolved) do not cause the loop to reprocess the same batch indefinitely.
        var processedContentItemIds = new HashSet<string>(StringComparer.Ordinal);

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var manager = scope.ServiceProvider.GetRequiredService<IPhoneNumberVerificationManager>();
            var now = clock.UtcNow;

            var batch = await session.Query<ContentItem, PhoneNumberVerificationPartIndex>(index =>
                    index.NextVerificationDueUtc == null || index.NextVerificationDueUtc <= now)
                .OrderBy(index => index.ContentItemId)
                .Take(BatchSize)
                .ListAsync(cancellationToken);

            var pending = batch
                .Where(contentItem => processedContentItemIds.Add(contentItem.ContentItemId))
                .ToList();

            if (pending.Count == 0)
            {
                break;
            }

            foreach (var contentItem in pending)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await manager.VerifyContentItemAsync(contentItem, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to revalidate the phone number for content item '{ContentItemId}'.", contentItem.ContentItemId);
                }
            }

            await session.SaveChangesAsync(cancellationToken);
        }
    }
}
