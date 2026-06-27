using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Controllers;

/// <summary>
/// Admin controller that exposes the phone number verification records queue, allowing administrators
/// to search records by phone number, filter by status, and manually re-queue exhausted records.
/// </summary>
[Admin]
public sealed class RecordsController : Controller
{
    private const int RequeueBatchSize = 100;
    private const int MaxRetryAllFailedRecords = 10_000;

    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;
    private readonly INotifier _notifier;
    private readonly IPhoneNumberVerificationManager _verificationManager;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="session">The YesSql session used to query the verification index.</param>
    /// <param name="siteService">The site service used to read module settings.</param>
    /// <param name="shapeFactory">The shape factory used to build the pager shape.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="verificationManager">The phone number verification manager used to check for enabled providers.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public RecordsController(
        IAuthorizationService authorizationService,
        ISession session,
        ISiteService siteService,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        INotifier notifier,
        IPhoneNumberVerificationManager verificationManager,
        IStringLocalizer<RecordsController> stringLocalizer,
        IHtmlLocalizer<RecordsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _session = session;
        _siteService = siteService;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
        _notifier = notifier;
        _verificationManager = verificationManager;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Renders the searchable, paged phone number verification queue dashboard.
    /// </summary>
    /// <param name="q">An optional phone number search term.</param>
    /// <param name="status">The status filter to apply.</param>
    /// <param name="sort">The sort order to apply.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>The verification queue view.</returns>
    public async Task<IActionResult> Index(
        string q,
        PhoneNumberVerificationRecordFilter status,
        PhoneNumberVerificationRecordSort sort,
        PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport))
        {
            return Forbid();
        }

        var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();
        var canVerifyPhoneNumbers = await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.VerifyPhoneNumbers);
        var maxAttempts = settings.MaxVerificationAttempts > 0
            ? settings.MaxVerificationAttempts
            : PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts;

        var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var pager = new Pager(pagerParameters, _pagerOptions.GetPageSize());

        var query = ApplyStatusFilter(BuildBaseQuery(term), status, maxAttempts);

        var totalCount = await query.CountAsync();

        var contentItems = await ApplySort(query, sort)
            .Skip(pager.GetStartIndex())
            .Take(pager.PageSize)
            .ListAsync();

        var routeData = new RouteData();

        if (term is not null)
        {
            routeData.Values[nameof(q)] = term;
        }

        if (status != PhoneNumberVerificationRecordFilter.All)
        {
            routeData.Values[nameof(status)] = status;
        }

        if (sort != PhoneNumberVerificationRecordSort.RecentlyAttempted)
        {
            routeData.Values[nameof(sort)] = sort;
        }

        var pagerShape = await _shapeFactory.PagerAsync(pager, totalCount, routeData);

        var entries = new List<PhoneNumberVerificationRecordEntry>();

        foreach (var contentItem in contentItems)
        {
            if (!contentItem.TryGet<PhoneNumberVerificationPart>(out var part))
            {
                continue;
            }

            entries.Add(new PhoneNumberVerificationRecordEntry
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
            });
        }

        var model = new PhoneNumberVerificationRecordsViewModel
        {
            Q = q,
            Status = status,
            Sort = sort,
            CanVerifyPhoneNumbers = canVerifyPhoneNumbers,
            Entries = entries,
            Pager = pagerShape,
            Sorts = BuildSortOptions(),
            Counts = await BuildStatusCountsAsync(maxAttempts, term),
        };

        return View(model);
    }

    /// <summary>
    /// Re-queues a single record for verification by resetting its failure counters and marking it
    /// pending. Deferred throttled work re-verifies queued records shortly afterwards, which avoids
    /// blocking the request and respects provider rate limits.
    /// </summary>
    /// <param name="contentItemId">The identifier of the content item to re-queue.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    /// <returns>A redirect to the originating page or the records queue.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(string contentItemId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.VerifyPhoneNumbers))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(contentItemId))
        {
            return NotFound();
        }

        if (!await EnsureProvidersEnabledAsync())
        {
            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        var requeued = await RequeueContentItemsAsync([contentItemId]);

        if (requeued == 0)
        {
            return NotFound();
        }

        await _notifier.SuccessAsync(H["The record was queued for verification and will be processed shortly."]);

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    /// <summary>
    /// Re-queues the selected records for verification. Deferred throttled work re-verifies the
    /// queued records shortly afterwards.
    /// </summary>
    /// <param name="contentItemIds">The identifiers of the records selected on the current page.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    /// <returns>A redirect to the originating page or the records queue.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetrySelected(string[] contentItemIds, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.VerifyPhoneNumbers))
        {
            return Forbid();
        }

        if (contentItemIds is null || contentItemIds.Length == 0)
        {
            await _notifier.WarningAsync(H["No records were selected."]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        if (!await EnsureProvidersEnabledAsync())
        {
            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        var requeued = await RequeueContentItemsAsync(contentItemIds);

        await _notifier.SuccessAsync(H["{0} record(s) were queued for verification and will be processed shortly.", requeued]);

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    /// <summary>
    /// Re-queues failed and needs-attention records across all pages, honoring the current search term.
    /// Deferred throttled work re-verifies the queued records shortly afterwards.
    /// </summary>
    /// <param name="q">The optional search term used to scope which failed records are re-queued.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    /// <returns>A redirect to the originating page or the records queue.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryAllFailed(string q, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.VerifyPhoneNumbers))
        {
            return Forbid();
        }

        if (!await EnsureProvidersEnabledAsync())
        {
            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var failedContentItemIds = await GetFailedContentItemIdsAsync(term);

        if (failedContentItemIds.Count == 0)
        {
            await _notifier.InformationAsync(H["There are no failed records to retry."]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        var requeued = await RequeueContentItemsAsync(failedContentItemIds);

        await _notifier.SuccessAsync(H["{0} failed or needs-attention record(s) were queued for verification and will be processed shortly.", requeued]);

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    private async Task<bool> EnsureProvidersEnabledAsync()
    {
        if ((await _verificationManager.GetEnabledProvidersAsync()).Count == 0)
        {
            await _notifier.WarningAsync(H["No phone number verification providers are enabled. Enable a provider before retrying."]);

            return false;
        }

        return true;
    }

    private async Task<int> RequeueContentItemsAsync(IEnumerable<string> contentItemIds)
    {
        var ids = contentItemIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return 0;
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

        if (requeuedContentItemIds.Count > 0)
        {
            ShellScope.AddDeferredTask(scope => ProcessRequeuedRecordsAsync(scope, requeuedContentItemIds));
        }

        return requeuedContentItemIds.Count;
    }

    private async Task<IList<string>> GetFailedContentItemIdsAsync(string term)
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

    private async Task<IDictionary<PhoneNumberVerificationRecordFilter, int>> BuildStatusCountsAsync(int maxAttempts, string term)
    {
        var counts = new Dictionary<PhoneNumberVerificationRecordFilter, int>();

        foreach (var filter in Enum.GetValues<PhoneNumberVerificationRecordFilter>())
        {
            counts[filter] = await ApplyStatusFilter(BuildBaseQuery(term), filter, maxAttempts).CountAsync();
        }

        return counts;
    }

    private IList<SelectListItem> BuildSortOptions()
    {
        return
        [
            new SelectListItem(S["Recently attempted"], nameof(PhoneNumberVerificationRecordSort.RecentlyAttempted)),
            new SelectListItem(S["Least recently attempted"], nameof(PhoneNumberVerificationRecordSort.LeastRecentlyAttempted)),
            new SelectListItem(S["Recently created"], nameof(PhoneNumberVerificationRecordSort.RecentlyCreated)),
            new SelectListItem(S["Oldest created"], nameof(PhoneNumberVerificationRecordSort.OldestCreated)),
        ];
    }

    private IActionResult RedirectToReturnUrlOrIndex(string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
