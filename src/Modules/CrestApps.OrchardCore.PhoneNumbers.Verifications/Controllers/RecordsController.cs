using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Controllers;

/// <summary>
/// Admin controller that exposes the phone number verification records queue, allowing administrators
/// to search records by phone number, filter by status, and manually re-queue exhausted records.
/// </summary>
[Admin]
public sealed class RecordsController : Controller
{
    private const string RetryBulkAction = "Retry";

    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;
    private readonly INotifier _notifier;
    private readonly IPhoneNumberVerificationManager _verificationManager;
    private readonly PhoneNumberVerificationRecordsService _recordsService;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shapeFactory">The shape factory used to build the pager shape.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="verificationManager">The phone number verification manager used to check for enabled providers.</param>
    /// <param name="recordsService">The records queue, which searches, counts and re-queues the records.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public RecordsController(
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        INotifier notifier,
        IPhoneNumberVerificationManager verificationManager,
        PhoneNumberVerificationRecordsService recordsService,
        IStringLocalizer<RecordsController> stringLocalizer,
        IHtmlLocalizer<RecordsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
        _notifier = notifier;
        _verificationManager = verificationManager;
        _recordsService = recordsService;
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

        var canVerifyPhoneNumbers = await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.VerifyPhoneNumbers);
        var maxAttempts = await _recordsService.GetMaxAttemptsAsync();

        var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var pager = new Pager(pagerParameters, _pagerOptions);

        var (totalCount, records) = await _recordsService.ListAsync(term, status, sort, pager.GetStartIndex(), pager.PageSize, maxAttempts);

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

        var model = new PhoneNumberVerificationRecordsViewModel
        {
            Q = q,
            Status = status,
            Sort = sort,
            CanVerifyPhoneNumbers = canVerifyPhoneNumbers,
            Pager = pagerShape,
            Sorts = BuildSortOptions(),
            Counts = await _recordsService.CountByStatusAsync(maxAttempts, term),

            // The admin-list tag builds a row per record, in the layout (List or Grid) the site or the user picked.
            // The selected records are re-queued through the toolbar's bulk action, so only a user who can retry
            // verifications can select them.
            List = new AdminListContext<PhoneNumberVerificationRecord>
            {
                Source = [.. records],
                Pager = pagerShape,
                ShowSelectAll = canVerifyPhoneNumbers,
                BulkActions = canVerifyPhoneNumbers
                    ? [new SelectListItem(S["Retry"], RetryBulkAction)]
                    : null,
            },
        };

        // The sort renders at the end of the list's toolbar.
        model.List.ToolbarActions = await _shapeFactory.CreateAsync("PhoneNumberVerificationRecordsListFilters", Arguments.From(new
        {
            model.Sort,
            model.Sorts,
        }));

        return View(model);
    }

    /// <summary>
    /// Applies the search and the sort order posted by the records queue, keeping the status filter.
    /// </summary>
    /// <param name="q">The phone number search term.</param>
    /// <param name="status">The status filter to keep.</param>
    /// <param name="sort">The sort order to apply.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered records queue.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    public IActionResult IndexFilterPost(
        string q,
        PhoneNumberVerificationRecordFilter status,
        PhoneNumberVerificationRecordSort sort,
        PagerParameters pagerParameters)
    {
        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeValues[nameof(q)] = q.Trim();
        }

        if (status != PhoneNumberVerificationRecordFilter.All)
        {
            routeValues[nameof(status)] = status;
        }

        if (sort != PhoneNumberVerificationRecordSort.RecentlyAttempted)
        {
            routeValues[nameof(sort)] = sort;
        }

        if (pagerParameters.PageSize.HasValue)
        {
            routeValues["pageSize"] = pagerParameters.PageSize;
        }

        return RedirectToAction(nameof(Index), routeValues);
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

        var requeued = await _recordsService.RequeueAsync([contentItemId], processAfterRequest: true);

        if (requeued.Count == 0)
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
    /// <param name="itemIds">The identifiers of the records selected on the current page.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    /// <returns>A redirect to the originating page or the records queue.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetrySelected(string[] itemIds, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PhoneNumberVerificationsPermissions.VerifyPhoneNumbers))
        {
            return Forbid();
        }

        if (itemIds is null || itemIds.Length == 0)
        {
            await _notifier.WarningAsync(H["No records were selected."]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        if (!await EnsureProvidersEnabledAsync())
        {
            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        var requeued = await _recordsService.RequeueAsync(itemIds, processAfterRequest: true);

        await _notifier.SuccessAsync(H["{0} record(s) were queued for verification and will be processed shortly.", requeued.Count]);

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

        var failedContentItemIds = await _recordsService.GetFailedContentItemIdsAsync(term);

        if (failedContentItemIds.Count == 0)
        {
            await _notifier.InformationAsync(H["There are no failed records to retry."]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        var requeued = await _recordsService.RequeueAsync(failedContentItemIds, processAfterRequest: true);

        await _notifier.SuccessAsync(H["{0} failed or needs-attention record(s) were queued for verification and will be processed shortly.", requeued.Count]);

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
