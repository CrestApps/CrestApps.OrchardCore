using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<PhoneNumberRevalidationBackgroundTask>>();

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
            var now = clock.UtcNow;

            var dueIndexes = await session.QueryIndex<PhoneNumberVerificationPartIndex>(index =>
                    (index.PhoneNumber != null || index.NormalizedPhoneNumber != null)
                    && index.FailedAttemptCount < maxAttempts
                    && (index.NextVerificationDueUtc == null || index.NextVerificationDueUtc <= now))
                .OrderBy(index => index.ContentItemId)
                .Take(MaxItemsPerRun)
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

        // Throttle provider calls across the whole run so a large backlog (for example, many records
        // re-queued at once) does not exceed provider rate limits and trigger HTTP 429 responses.
        var isFirstVerification = true;

        foreach (var contentItemIds in dueContentItemIds.Chunk(BatchSize))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = serviceProvider.CreateAsyncScope();

            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var verificationManager = scope.ServiceProvider.GetRequiredService<IPhoneNumberVerificationManager>();
            var settings = await siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

            var contentItems = await session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest && index.ContentItemId.IsIn(contentItemIds))
                .ListAsync(cancellationToken);

            foreach (var contentItem in contentItems)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var phoneNumber = GetStoredPhoneNumber(contentItem);

                    if (string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        continue;
                    }

                    if (!isFirstVerification && settings.RequestDelayMilliseconds > 0)
                    {
                        await Task.Delay(settings.RequestDelayMilliseconds, cancellationToken);
                    }

                    isFirstVerification = false;

                    var result = await verificationManager.VerifyAsync(phoneNumber, cancellationToken: cancellationToken);

                    contentItem.AlterPhoneNumberVerificationResult(
                        result,
                        revalidationIntervalDays: settings.RevalidationIntervalDays);

                    if (OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumberContentItem(contentItem) is { } phoneNumberContentItem)
                    {
                        phoneNumberContentItem.AlterPhoneNumberVerificationResult(
                            result,
                            revalidationIntervalDays: settings.RevalidationIntervalDays);
                    }

                    await session.SaveAsync(contentItem);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to revalidate the phone number for content item '{ContentItemId}'.", contentItem.ContentItemId);
                }
            }

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GetStoredPhoneNumber(ContentItem contentItem)
    {
        if (!contentItem.TryGet<PhoneNumberVerificationPart>(out var part))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(part.NormalizedPhoneNumber))
        {
            return part.NormalizedPhoneNumber;
        }

        if (!string.IsNullOrWhiteSpace(part.PhoneNumber))
        {
            return part.PhoneNumber;
        }

        return part.TryGetPhoneNumberVerificationResult(out var result)
            ? result.NormalizedPhoneNumber ?? result.PhoneNumber
            : null;
    }
}
