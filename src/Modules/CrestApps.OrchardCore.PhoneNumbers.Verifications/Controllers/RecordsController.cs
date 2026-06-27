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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Controllers;

/// <summary>
/// Admin controller that exposes the phone number verification records queue, allowing administrators
/// to search records by phone number, filter by status, and manually re-queue exhausted records.
/// </summary>
[Admin]
public sealed class RecordsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;
    private readonly INotifier _notifier;
    private readonly IPhoneNumberVerificationManager _verificationManager;
    private readonly ILogger _logger;

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
    /// <param name="verificationManager">The phone number verification manager used to re-verify records on demand.</param>
    /// <param name="logger">The logger.</param>
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
        ILogger<RecordsController> logger,
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
        _logger = logger;
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
            Entries = entries,
            Pager = pagerShape,
            Sorts = BuildSortOptions(),
            Counts = await BuildStatusCountsAsync(maxAttempts, term),
        };

        return View(model);
    }

    /// <summary>
    /// Immediately re-verifies a record against the configured provider, resets its failure counters,
    /// updates its status, and reports the outcome instead of waiting for the daily background task.
    /// </summary>
    /// <param name="contentItemId">The identifier of the content item to re-verify.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    /// <returns>A redirect to the originating page or the records queue.</returns>
    [HttpPost]
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

        var contentItem = await _session.Query<ContentItem, ContentItemIndex>(index =>
                index.Latest && index.ContentItemId == contentItemId)
            .FirstOrDefaultAsync();

        if (contentItem is null || !contentItem.TryGet<PhoneNumberVerificationPart>(out var part))
        {
            return NotFound();
        }

        var phoneNumber = GetStoredPhoneNumber(part);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            await _notifier.WarningAsync(H["This record does not have a phone number to verify."]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        if ((await _verificationManager.GetEnabledProvidersAsync()).Count == 0)
        {
            await _notifier.WarningAsync(H["No phone number verification providers are enabled. Enable a provider before retrying."]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

        contentItem.RequeuePhoneNumberVerification();

        try
        {
            var result = await _verificationManager.VerifyAsync(phoneNumber);

            contentItem.AlterPhoneNumberVerificationResult(
                result,
                revalidationIntervalDays: settings.RevalidationIntervalDays);

            if (OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumberContentItem(contentItem) is { } phoneNumberContentItem)
            {
                phoneNumberContentItem.AlterPhoneNumberVerificationResult(
                    result,
                    revalidationIntervalDays: settings.RevalidationIntervalDays);
            }

            await _session.SaveAsync(contentItem);

            switch (result.Status)
            {
                case PhoneNumberVerificationStatus.Verified:
                    await _notifier.SuccessAsync(H["The phone number was verified successfully."]);
                    break;
                case PhoneNumberVerificationStatus.Invalid:
                    await _notifier.WarningAsync(H["The phone number was checked and is not valid."]);
                    break;
                default:
                    await _notifier.ErrorAsync(H["The verification request could not be completed: {0}", result.ErrorMessage]);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-verify the phone number for content item '{ContentItemId}'.", contentItem.ContentItemId);

            await _session.SaveAsync(contentItem);

            await _notifier.ErrorAsync(H["The verification request could not be completed. See the logs for details."]);
        }

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    private static string GetStoredPhoneNumber(PhoneNumberVerificationPart part)
    {
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
        => status switch
        {
            PhoneNumberVerificationRecordFilter.Verified => query.With<PhoneNumberVerificationPartIndex>(index => index.IsVerified),
            PhoneNumberVerificationRecordFilter.Invalid => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Invalid),
            PhoneNumberVerificationRecordFilter.Failed => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Failed),
            PhoneNumberVerificationRecordFilter.Pending => query.With<PhoneNumberVerificationPartIndex>(index => index.LastVerifiedUtc == null),
            PhoneNumberVerificationRecordFilter.NeedsAttention => query.With<PhoneNumberVerificationPartIndex>(index => index.FailedAttemptCount >= maxAttempts),
            _ => query,
        };

    private static IQuery<ContentItem> ApplySort(
        IQuery<ContentItem, PhoneNumberVerificationPartIndex> query,
        PhoneNumberVerificationRecordSort sort)
        => sort switch
        {
            PhoneNumberVerificationRecordSort.LeastRecentlyAttempted => query.OrderBy(index => index.LastAttemptUtc).ThenBy(index => index.ContentItemId),
            PhoneNumberVerificationRecordSort.RecentlyCreated => query.With<ContentItemIndex>().OrderByDescending(index => index.CreatedUtc).ThenBy(index => index.ContentItemId),
            PhoneNumberVerificationRecordSort.OldestCreated => query.With<ContentItemIndex>().OrderBy(index => index.CreatedUtc).ThenBy(index => index.ContentItemId),
            _ => query.OrderByDescending(index => index.LastAttemptUtc).ThenBy(index => index.ContentItemId),
        };

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
