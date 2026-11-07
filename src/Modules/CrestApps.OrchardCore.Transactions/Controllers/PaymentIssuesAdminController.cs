using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// Lists the money operations that failed and must be finished by hand, such as a refund the checkout could not issue
/// or a gateway subscription that could not be canceled, and records that an operator finished one.
/// </summary>
[Admin("payment-issues/{action}/{itemId?}", "PaymentIssues{action}")]
[RequireFeatures(CheckoutConstants.Features.Area)]
public sealed class PaymentIssuesAdminController : Controller
{
    private readonly IPaymentIssueStore _issueStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentIssuesAdminController"/> class.
    /// </summary>
    /// <param name="issueStore">The payment issues.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface outcomes.</param>
    /// <param name="clock">The clock used to stamp a resolution.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentIssuesAdminController(
        IPaymentIssueStore issueStore,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<PaymentIssuesAdminController> htmlLocalizer,
        IStringLocalizer<PaymentIssuesAdminController> stringLocalizer)
    {
        _issueStore = issueStore;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays the payment issues, the open ones by default.
    /// </summary>
    /// <param name="model">The filter.</param>
    /// <param name="all">Whether every issue is listed, rather than the open ones.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("payment-issues", "PaymentIssuesIndex")]
    public async Task<IActionResult> Index(
        PaymentIssuesAdminIndexViewModel model,
        bool all,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        // Open issues are what needs doing, so they are what the page shows first.
        var status = all ? model.Status : model.Status ?? PaymentIssueStatus.Open;
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _issueStore.PageAsync(pager.Page, pager.PageSize, status, HttpContext.RequestAborted);

        var routeData = new RouteData();

        if (status.HasValue)
        {
            routeData.Values.TryAdd("Status", status.Value);
        }
        else
        {
            routeData.Values.TryAdd("all", true);
        }

        var statuses = new List<SelectListItem>
        {
            new() { Text = S["Any status"], Value = string.Empty, Selected = !status.HasValue },
            new() { Text = S["Open"], Value = nameof(PaymentIssueStatus.Open), Selected = status == PaymentIssueStatus.Open },
            new() { Text = S["Resolved"], Value = nameof(PaymentIssueStatus.Resolved), Selected = status == PaymentIssueStatus.Resolved },
        };

        var filters = await shapeFactory.CreateAdminListFiltersAsync("PaymentIssues", "payment-issues-filter", new AdminListFilter("Status", statuses, S["Filter by status"], S["Status"]));

        model.Status = status;
        model.List = new AdminListContext<PaymentIssue>
        {
            Source = result.Entries,
            ToolbarActions = filters,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        return View(model);
    }

    /// <summary>
    /// Keeps the status filter when the toolbar is submitted.
    /// </summary>
    /// <param name="model">The filter.</param>
    /// <param name="pagerParameters">The page size to keep while filtering.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("payment-issues", "PaymentIssuesIndex")]
    public async Task<IActionResult> IndexFilterPost(PaymentIssuesAdminIndexViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (model.Status.HasValue)
        {
            routeValues.TryAdd("Status", model.Status.Value);
        }
        else
        {
            routeValues.TryAdd("all", true);
        }

        if (pagerParameters.PageSize.HasValue)
        {
            routeValues.TryAdd("pageSize", pagerParameters.PageSize.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Records that an operator finished an issue by hand.
    /// </summary>
    /// <param name="itemId">The issue's ID.</param>
    /// <param name="note">What the operator did, such as the reference of a manual refund.</param>
    [HttpPost]
    public async Task<IActionResult> Resolve(string itemId, string note)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(itemId) || await _issueStore.FindByIdAsync(itemId, HttpContext.RequestAborted) is not { } issue)
        {
            return NotFound();
        }

        if (issue.Status == PaymentIssueStatus.Open)
        {
            issue.Status = PaymentIssueStatus.Resolved;
            issue.ResolvedUtc = _clock.UtcNow;
            issue.ResolvedBy = User.Identity?.Name;
            issue.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            await _issueStore.UpdateAsync(issue, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The payment issue was marked as resolved."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
