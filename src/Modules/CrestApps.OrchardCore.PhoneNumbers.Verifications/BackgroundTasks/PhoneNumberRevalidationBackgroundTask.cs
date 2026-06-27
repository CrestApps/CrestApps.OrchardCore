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
/// is due. Work is processed in resilient batches so the task scales and tolerates provider failures.
/// </summary>
[BackgroundTask(
    Title = "Phone Number Revalidation",
    Schedule = "0 3 * * *",
    Description = "Revalidates contact phone numbers that are due for verification.",
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

            if (verificationManager.GetProviders().Count == 0)
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
