using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Core;
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
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// The administration ledger of refunds, including the ones a provider cannot settle on its own.
/// </summary>
/// <remarks>
/// A refund against a payment method with no executable refund operation is recorded as pending manual
/// review rather than silently dropped, which means somebody has to go and move the money by hand. Without
/// a screen listing those, they sit in the database and the customer never gets paid back. This is that
/// screen, and the place an operator records that they did.
/// </remarks>
[Admin("refunds/{action}/{itemId?}", "Refunds{action}")]
public sealed class RefundsAdminController : Controller
{
    private readonly IPaymentRefundStore _refundStore;
    private readonly IEnumerable<ICheckoutPaymentProvider> _paymentProviders;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefundsAdminController"/> class.
    /// </summary>
    /// <param name="refundStore">The durable refund ledger.</param>
    /// <param name="paymentProviders">The registered payment providers, used to build the method filter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface outcomes.</param>
    /// <param name="clock">The clock used to stamp a manual resolution.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RefundsAdminController(
        IPaymentRefundStore refundStore,
        IEnumerable<ICheckoutPaymentProvider> paymentProviders,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<RefundsAdminController> htmlLocalizer,
        IStringLocalizer<RefundsAdminController> stringLocalizer)
    {
        _refundStore = refundStore;
        _paymentProviders = paymentProviders;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays the refunds ledger.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("refunds", "RefundsIndex")]
    public async Task<IActionResult> Index(
        RefundsAdminIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _refundStore.PageAsync(pager.Page, pager.PageSize, new PaymentRefundQuery
        {
            ProviderKey = options.ProviderKey,
            Status = options.Status,
            SessionId = options.SessionId,
        });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.ProviderKey))
        {
            routeData.Values.TryAdd("Options.ProviderKey", options.ProviderKey);
        }

        if (options.Status.HasValue)
        {
            routeData.Values.TryAdd("Options.Status", options.Status.Value);
        }

        options.Statuses = BuildStatusItems(options.Status);
        options.Providers = BuildProviderItems(options.ProviderKey);

        return View(new RefundsAdminIndexViewModel
        {
            Options = options,
            Refunds = [.. result.Entries],
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        });
    }

    /// <summary>
    /// Preserves the ledger filter when the toolbar is submitted.
    /// </summary>
    /// <param name="options">The filter options.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("refunds", "RefundsIndex")]
    public async Task<IActionResult> IndexFilterPost(RefundsAdminIndexOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options.ProviderKey))
        {
            routeValues.TryAdd("Options.ProviderKey", options.ProviderKey);
        }

        if (options.Status.HasValue)
        {
            routeValues.TryAdd("Options.Status", options.Status.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Records that a refund awaiting manual review was paid back outside the application.
    /// </summary>
    /// <param name="itemId">The refund identifier.</param>
    /// <param name="reference">The operator's reference for the money they moved.</param>
    [HttpPost]
    public async Task<IActionResult> Resolve(string itemId, string reference)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        var refund = string.IsNullOrEmpty(itemId) ? null : await _refundStore.FindByIdAsync(itemId);

        if (refund is null)
        {
            return NotFound();
        }

        // Only a refund the system could not settle itself may be closed by hand. Allowing it on any refund
        // would let an operator mark a failed gateway refund as done when the customer got nothing.
        if (refund.Status != RefundStatus.PendingManualReview)
        {
            await _notifier.WarningAsync(H["Only a refund awaiting manual review can be resolved by hand."]);

            return RedirectToAction(nameof(Index));
        }

        refund.Status = RefundStatus.Succeeded;
        refund.CompletedUtc = _clock.UtcNow;
        refund.ProviderRefundReference = string.IsNullOrWhiteSpace(reference) ? refund.ProviderRefundReference : reference.Trim();

        await _refundStore.UpdateAsync(refund);

        await _notifier.SuccessAsync(H["The refund was marked as settled."]);

        return RedirectToAction(nameof(Index));
    }

    private List<SelectListItem> BuildStatusItems(RefundStatus? selected)
    {
        var items = new List<SelectListItem>
        {
            new() { Text = S["All statuses"], Value = string.Empty, Selected = !selected.HasValue },
        };

        foreach (var status in Enum.GetValues<RefundStatus>())
        {
            items.Add(new SelectListItem
            {
                Text = status.ToString(),
                Value = status.ToString(),
                Selected = selected == status,
            });
        }

        return items;
    }

    private List<SelectListItem> BuildProviderItems(string selected)
    {
        var items = new List<SelectListItem>
        {
            new() { Text = S["All methods"], Value = string.Empty, Selected = string.IsNullOrEmpty(selected) },
        };

        foreach (var provider in _paymentProviders)
        {
            items.Add(new SelectListItem
            {
                Text = provider.DisplayName ?? provider.Key,
                Value = provider.Key,
                Selected = string.Equals(provider.Key, selected, StringComparison.OrdinalIgnoreCase),
            });
        }

        return items;
    }
}
