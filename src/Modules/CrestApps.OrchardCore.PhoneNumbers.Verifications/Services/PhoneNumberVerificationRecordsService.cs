using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// The phone number verification records queue: the search, status filter, counts and sort of the records page, and the
/// re-queueing of records for another verification. The records page and the AI tools both call it.
/// </summary>
public sealed class PhoneNumberVerificationRecordsService
{
    /// <summary>
    /// The most failed records one "retry all failed" re-queues.
    /// </summary>
    public const int MaxRetryAllFailedRecords = 10_000;

    private const int RequeueBatchSize = 100;

    private readonly ISession _session;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationRecordsService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="siteService">The site service.</param>
    public PhoneNumberVerificationRecordsService(ISession session, ISiteService siteService)
    {
        _session = session;
        _siteService = siteService;
    }

    /// <summary>
    /// Gets the number of failed attempts after which a record is no longer retried automatically.
    /// </summary>
    /// <returns>The maximum number of failed attempts.</returns>
    public async Task<int> GetMaxAttemptsAsync()
    {
        var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

        return settings.MaxVerificationAttempts > 0
            ? settings.MaxVerificationAttempts
            : PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts;
    }

    /// <summary>
    /// Lists one page of records.
    /// </summary>
    /// <param name="term">Text in the phone number, or <see langword="null"/>.</param>
    /// <param name="status">The status filter.</param>
    /// <param name="sort">The sort order.</param>
    /// <param name="skip">The records to skip.</param>
    /// <param name="take">The records to return.</param>
    /// <param name="maxAttempts">The maximum number of failed attempts.</param>
    /// <returns>The number of matching records and the page.</returns>
    public async Task<(int Total, IReadOnlyList<PhoneNumberVerificationRecord> Records)> ListAsync(
        string term,
        PhoneNumberVerificationRecordFilter status,
        PhoneNumberVerificationRecordSort sort,
        int skip,
        int take,
        int maxAttempts)
    {
        var query = ApplyStatusFilter(BuildBaseQuery(term), status, maxAttempts);
        var total = await query.CountAsync();
        var contentItems = await ApplySort(query, sort)
            .Skip(skip)
            .Take(take)
            .ListAsync();

        var records = new List<PhoneNumberVerificationRecord>();

        foreach (var contentItem in contentItems)
        {
            if (ToRecord(contentItem, maxAttempts) is { } record)
            {
                records.Add(record);
            }
        }

        return (total, records);
    }

    /// <summary>
    /// Counts the records of every status filter.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of failed attempts.</param>
    /// <param name="term">Text in the phone number, or <see langword="null"/>.</param>
    /// <returns>The count of each status filter.</returns>
    public async Task<IDictionary<PhoneNumberVerificationRecordFilter, int>> CountByStatusAsync(int maxAttempts, string term)
    {
        var counts = new Dictionary<PhoneNumberVerificationRecordFilter, int>();

        foreach (var filter in Enum.GetValues<PhoneNumberVerificationRecordFilter>())
        {
            counts[filter] = await ApplyStatusFilter(BuildBaseQuery(term), filter, maxAttempts).CountAsync();
        }

        return counts;
    }

    /// <summary>
    /// Re-queues records for verification: their failure counters are cleared and they are marked pending, so the
    /// Phone Number Revalidation background task verifies them at its next run.
    /// </summary>
    /// <param name="contentItemIds">The content items.</param>
    /// <param name="processAfterRequest">Whether to verify them in deferred work once the current scope ends, with the
    /// configured request delay, as the records page does.</param>
    /// <returns>The content items that were re-queued.</returns>
    public async Task<IReadOnlyList<string>> RequeueAsync(IEnumerable<string> contentItemIds, bool processAfterRequest)
    {
        var ids = (contentItemIds ?? [])
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var requeuedContentItemIds = new List<string>();

        foreach (var chunk in ids.Chunk(RequeueBatchSize))
        {
            var contentItems = await _session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest && index.ContentItemId.IsIn(chunk))
                .ListAsync();

            foreach (var contentItem in contentItems)
            {
                if (!contentItem.Has<PhoneNumberVerificationPart>())
                {
                    continue;
                }

                contentItem.RequeuePhoneNumberVerification();

                await _session.SaveAsync(contentItem);

                requeuedContentItemIds.Add(contentItem.ContentItemId);
            }
        }

        if (processAfterRequest && requeuedContentItemIds.Count > 0)
        {
            ShellScope.AddDeferredTask(scope => ProcessRequeuedRecordsAsync(scope, requeuedContentItemIds));
        }

        return requeuedContentItemIds;
    }

    /// <summary>
    /// Gets the content items whose last verification failed, up to <see cref="MaxRetryAllFailedRecords"/>.
    /// </summary>
    /// <param name="term">Text in the phone number, or <see langword="null"/>.</param>
    /// <returns>The content item ids.</returns>
    public async Task<IList<string>> GetFailedContentItemIdsAsync(string term)
    {
        var query = _session.QueryIndex<PhoneNumberVerificationPartIndex>(index =>
            index.VerificationStatus == PhoneNumberVerificationStatus.Failed);

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(index => index.PhoneNumber.Contains(term) || index.NormalizedPhoneNumber.Contains(term));
        }

        var indexes = await query
            .OrderBy(index => index.ContentItemId)
            .Take(MaxRetryAllFailedRecords)
            .ListAsync();

        return indexes
            .Select(index => index.ContentItemId)
            .Where(contentItemId => !string.IsNullOrEmpty(contentItemId))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Builds a record of the queue from a content item.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="maxAttempts">The maximum number of failed attempts.</param>
    /// <returns>The record, or <see langword="null"/> when the item has no verification part.</returns>
    public static PhoneNumberVerificationRecord ToRecord(ContentItem contentItem, int maxAttempts)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        if (!contentItem.TryGet<PhoneNumberVerificationPart>(out var part))
        {
            return null;
        }

        part.TryGetPhoneNumberVerificationResult(out var result);

        return new PhoneNumberVerificationRecord
        {
            ContentItemId = contentItem.ContentItemId,
            DisplayText = contentItem.DisplayText,
            ContentType = contentItem.ContentType,
            PhoneNumber = part.PhoneNumber,
            NormalizedPhoneNumber = part.NormalizedPhoneNumber ?? part.PhoneNumber,
            VerificationStatus = part.VerificationStatus,
            VerificationProvider = part.VerificationProvider,
            LastVerifiedUtc = part.LastVerifiedUtc,
            LastAttemptUtc = part.LastAttemptUtc,
            NextVerificationDueUtc = part.NextVerificationDueUtc,
            VerificationAttemptCount = part.VerificationAttemptCount,
            FailedAttemptCount = part.FailedAttemptCount,
            LastError = part.LastError,
            IsExhausted = part.HasReachedMaxVerificationAttempts(maxAttempts),
            LineStatus = result?.LineStatus,
            MinimumAge = result?.MinimumAge,
        };
    }

    private static async Task ProcessRequeuedRecordsAsync(ShellScope scope, List<string> contentItemIds)
    {
        if (contentItemIds.Count == 0)
        {
            return;
        }

        var services = scope.ServiceProvider;
        var verificationManager = services.GetRequiredService<IPhoneNumberVerificationManager>();

        if ((await verificationManager.GetEnabledProvidersAsync()).Count == 0)
        {
            return;
        }

        var session = services.GetRequiredService<ISession>();
        var siteService = services.GetRequiredService<ISiteService>();
        var queueProcessor = services.GetRequiredService<IPhoneNumberVerificationQueueProcessor>();
        var settings = await siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();
        var delayBeforeNextRequest = false;

        foreach (var chunk in contentItemIds.Chunk(RequeueBatchSize))
        {
            var contentItems = await session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest && index.ContentItemId.IsIn(chunk))
                .ListAsync();

            var processed = await queueProcessor.ProcessAsync(contentItems, settings, delayBeforeNextRequest);

            if (processed > 0)
            {
                delayBeforeNextRequest = true;
            }

            foreach (var contentItem in contentItems)
            {
                await session.SaveAsync(contentItem);
            }

            await session.SaveChangesAsync();
        }
    }

    private IQuery<ContentItem, PhoneNumberVerificationPartIndex> BuildBaseQuery(string term)
    {
        var query = _session.Query<ContentItem, ContentItemIndex>(index => index.Latest)
            .With<PhoneNumberVerificationPartIndex>(index => index.PhoneNumber != null || index.NormalizedPhoneNumber != null);

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.With<PhoneNumberVerificationPartIndex>(index =>
                index.PhoneNumber.Contains(term) || index.NormalizedPhoneNumber.Contains(term));
        }

        return query;
    }

    private static IQuery<ContentItem, PhoneNumberVerificationPartIndex> ApplyStatusFilter(
        IQuery<ContentItem, PhoneNumberVerificationPartIndex> query,
        PhoneNumberVerificationRecordFilter status,
        int maxAttempts)
    {
        return status switch
        {
            PhoneNumberVerificationRecordFilter.Verified => query.With<PhoneNumberVerificationPartIndex>(index => index.IsVerified),
            PhoneNumberVerificationRecordFilter.Invalid => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Invalid),
            PhoneNumberVerificationRecordFilter.Pending => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Unverified),
            PhoneNumberVerificationRecordFilter.Failed => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Failed && index.FailedAttemptCount < maxAttempts),
            PhoneNumberVerificationRecordFilter.NeedsAttention => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Failed && index.FailedAttemptCount >= maxAttempts),
            _ => query,
        };
    }

    private static IQuery<ContentItem> ApplySort(
        IQuery<ContentItem, PhoneNumberVerificationPartIndex> query,
        PhoneNumberVerificationRecordSort sort)
    {
        return sort switch
        {
            PhoneNumberVerificationRecordSort.LeastRecentlyAttempted => query.OrderBy(index => index.LastAttemptUtc).ThenBy(index => index.ContentItemId),
            PhoneNumberVerificationRecordSort.RecentlyCreated => query.With<ContentItemIndex>().OrderByDescending(index => index.CreatedUtc).ThenBy(index => index.ContentItemId),
            PhoneNumberVerificationRecordSort.OldestCreated => query.With<ContentItemIndex>().OrderBy(index => index.CreatedUtc).ThenBy(index => index.ContentItemId),
            _ => query.OrderByDescending(index => index.LastAttemptUtc).ThenBy(index => index.ContentItemId),
        };
    }
}
