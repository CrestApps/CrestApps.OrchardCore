using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
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
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public RecordsController(
        IAuthorizationService authorizationService,
        ISession session,
        ISiteService siteService,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        INotifier notifier,
        IStringLocalizer<RecordsController> stringLocalizer,
        IHtmlLocalizer<RecordsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _session = session;
        _siteService = siteService;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
        _notifier = notifier;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Renders the searchable, paged phone number verification records queue.
    /// </summary>
    /// <param name="q">An optional phone number search term.</param>
    /// <param name="status">The status filter to apply.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>The records queue view.</returns>
    public async Task<IActionResult> Index(
        string q,
        PhoneNumberVerificationRecordFilter status,
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

        var pager = new Pager(pagerParameters, _pagerOptions.GetPageSize());

        var query = _session.Query<ContentItem, ContentItemIndex>(index => index.Latest)
            .With<PhoneNumberVerificationPartIndex>(index => index.PhoneNumber != null || index.NormalizedPhoneNumber != null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();

            query = query.With<PhoneNumberVerificationPartIndex>(index =>
                index.PhoneNumber.Contains(term) || index.NormalizedPhoneNumber.Contains(term));
        }

        query = status switch
        {
            PhoneNumberVerificationRecordFilter.Verified => query.With<PhoneNumberVerificationPartIndex>(index => index.IsVerified),
            PhoneNumberVerificationRecordFilter.Invalid => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Invalid),
            PhoneNumberVerificationRecordFilter.Failed => query.With<PhoneNumberVerificationPartIndex>(index => index.VerificationStatus == PhoneNumberVerificationStatus.Failed),
            PhoneNumberVerificationRecordFilter.Pending => query.With<PhoneNumberVerificationPartIndex>(index => index.LastVerifiedUtc == null),
            PhoneNumberVerificationRecordFilter.NeedsAttention => query.With<PhoneNumberVerificationPartIndex>(index => index.FailedAttemptCount >= maxAttempts),
            _ => query,
        };

        var totalCount = await query.CountAsync();

        var contentItems = await query
            .OrderByDescending(index => index.LastAttemptUtc)
            .ThenBy(index => index.ContentItemId)
            .Skip(pager.GetStartIndex())
            .Take(pager.PageSize)
            .ListAsync();

        var pagerShape = await _shapeFactory.PagerAsync(pager, totalCount);

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
            Entries = entries,
            Pager = pagerShape,
            Statuses = BuildStatusOptions(),
        };

        return View(model);
    }

    /// <summary>
    /// Re-queues a record that has exhausted its automatic verification attempts so the background task retries it.
    /// </summary>
    /// <param name="contentItemId">The identifier of the content item to re-queue.</param>
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

        if (contentItem is null || !contentItem.TryGet<PhoneNumberVerificationPart>(out _))
        {
            return NotFound();
        }

        contentItem.RequeuePhoneNumberVerification();

        await _session.SaveAsync(contentItem);

        await _notifier.SuccessAsync(H["The phone number verification has been re-queued and will be retried in the background."]);

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    private IList<SelectListItem> BuildStatusOptions()
    {
        return
        [
            new SelectListItem(S["All"], nameof(PhoneNumberVerificationRecordFilter.All)),
            new SelectListItem(S["Verified"], nameof(PhoneNumberVerificationRecordFilter.Verified)),
            new SelectListItem(S["Invalid"], nameof(PhoneNumberVerificationRecordFilter.Invalid)),
            new SelectListItem(S["Failed"], nameof(PhoneNumberVerificationRecordFilter.Failed)),
            new SelectListItem(S["Pending"], nameof(PhoneNumberVerificationRecordFilter.Pending)),
            new SelectListItem(S["Needs attention"], nameof(PhoneNumberVerificationRecordFilter.NeedsAttention)),
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
