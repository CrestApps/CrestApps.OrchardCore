using CrestApps.OrchardCore.PhoneNumberVerifications.Indexes;
using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Flows.Models;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.BackgroundTasks;

/// <summary>
/// Periodically revalidates content items that have stored phone numbers whose verification
/// is due. Work is processed in resilient batches so the task scales and tolerates provider failures.
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
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var verificationManager = scope.ServiceProvider.GetRequiredService<IPhoneNumberVerificationManager>();
            var settings = await siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();
            var now = clock.UtcNow;

            if (verificationManager.GetProviders().Count == 0)
            {
                break;
            }

            var batch = await session.Query<ContentItem, PhoneNumberVerificationPartIndex>(index =>
                    (index.PhoneNumber != null || index.NormalizedPhoneNumber != null)
                    && (index.NextVerificationDueUtc == null || index.NextVerificationDueUtc <= now))
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
                    var phoneNumber = GetPhoneNumber(contentItem);

                    if (string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        continue;
                    }

                    var result = await verificationManager.VerifyAsync(phoneNumber, cancellationToken: cancellationToken);

                    contentItem.AlterPhoneNumberVerificationResult(
                        result,
                        revalidationIntervalDays: settings.RevalidationIntervalDays);

                    if (GetPreferredPhoneNumberContentItem(contentItem) is { } phoneNumberContentItem)
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

    private static string GetPhoneNumber(ContentItem contentItem)
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

    private static ContentItem GetPreferredPhoneNumberContentItem(ContentItem contact)
    {
        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart)
            || bagPart.ContentItems is null
            || bagPart.ContentItems.Count == 0)
        {
            return null;
        }

        var phoneNumbers = new PriorityQueue<ContentItem, int>();

        foreach (var contentMethod in bagPart.ContentItems)
        {
            if (contentMethod.ContentType != OmnichannelConstants.ContentTypes.PhoneNumber
                || !contentMethod.TryGet<PhoneNumberInfoPart>(out var phonePart)
                || string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                continue;
            }

            var priority = GetPhoneNumberPriority(phonePart.Type?.Text);

            if (priority is null)
            {
                continue;
            }

            phoneNumbers.Enqueue(contentMethod, priority.Value);
        }

        return phoneNumbers.Count > 0
            ? phoneNumbers.Dequeue()
            : null;
    }

    private static int? GetPhoneNumberPriority(string type)
    {
        return type switch
        {
            "Cell" => 1,
            "Home" => 2,
            "Office" => 3,
            "Work" => 4,
            "Other" => 5,
            _ => null,
        };
    }
}
