using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.BackgroundTasks;

/// <summary>
/// Periodically revalidates content items that have stored phone numbers whose verification
/// is due, including records re-queued from the verification queue. Work is processed in resilient
/// batches and provider calls are throttled so the task scales and respects provider rate limits.
/// </summary>
[BackgroundTask(
    Title = "Phone Number Revalidation",
    Schedule = "*/5 * * * *",
    Description = "Revalidates contact phone numbers that are due for verification, including re-queued records.",
    LockTimeout = 3_000,
    LockExpiration = 1_800_000)]
public sealed class PhoneNumberRevalidationBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 50;

    // Upper bound on the number of due content items handled per run. This caps memory usage when a
    // large backlog becomes due at once; any remainder is picked up by the next scheduled run.
    private const int MaxItemsPerRun = 10_000;
    private const int MaxProcessingDurationMilliseconds = 1_500_000;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        string[] dueContentItemIds;

        // Snapshot the due content item ids up front. Processing a fixed snapshot guarantees forward
        // progress even when some items fail verification and remain due, which would otherwise keep
        // reappearing at the head of every page and stall a predicate-based pager.
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var verificationManager = scope.ServiceProvider.GetRequiredService<IPhoneNumberVerificationManager>();

            if ((await verificationManager.GetEnabledProvidersAsync(cancellationToken)).Count == 0)
            {
                return;
            }

            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var settings = await siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();
            var maxAttempts = settings.MaxVerificationAttempts > 0
                ? settings.MaxVerificationAttempts
                : PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts;
            var maxItemsPerRun = GetMaxItemsPerRun(settings.RequestDelayMilliseconds);
            var now = clock.UtcNow;

            var dueIndexes = await session.QueryIndex<PhoneNumberVerificationPartIndex>(index =>
                    (index.PhoneNumber != null || index.NormalizedPhoneNumber != null)
                    && index.FailedAttemptCount < maxAttempts
                    && (index.NextVerificationDueUtc == null || index.NextVerificationDueUtc <= now))
                .OrderBy(index => index.ContentItemId)
                .Take(maxItemsPerRun)
                .ListAsync(cancellationToken);

            dueContentItemIds = dueIndexes
                .Select(index => index.ContentItemId)
                .Where(contentItemId => !string.IsNullOrEmpty(contentItemId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (dueContentItemIds.Length == 0)
        {
            return;
        }

        var delayBeforeNextRequest = false;

        foreach (var contentItemIds in dueContentItemIds.Chunk(BatchSize))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = serviceProvider.CreateAsyncScope();

            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var queueProcessor = scope.ServiceProvider.GetRequiredService<IPhoneNumberVerificationQueueProcessor>();
            var settings = await siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

            var contentItems = await session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest && index.ContentItemId.IsIn(contentItemIds))
                .ListAsync(cancellationToken);

            var processed = await queueProcessor.ProcessAsync(
                contentItems,
                settings,
                delayBeforeNextRequest,
                cancellationToken);

            if (processed > 0)
            {
                delayBeforeNextRequest = true;
            }

            foreach (var contentItem in contentItems)
            {
                await session.SaveAsync(contentItem, cancellationToken: cancellationToken);
            }

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    private static int GetMaxItemsPerRun(int requestDelayMilliseconds)
    {
        if (requestDelayMilliseconds <= 0)
        {
            return MaxItemsPerRun;
        }

        return Math.Clamp(MaxProcessingDurationMilliseconds / requestDelayMilliseconds, BatchSize, MaxItemsPerRun);
    }
}
