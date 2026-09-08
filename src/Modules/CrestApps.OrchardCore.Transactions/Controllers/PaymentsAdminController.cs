using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
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
using OrchardCore.Navigation;
using OrchardCore.Routing;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// The administration ledger of payment attempts, and the form that refunds one.
/// </summary>
/// <remarks>
/// Every charge the suite makes already leaves a durable attempt behind, but until now there was no way to
/// look at them. An operator asked "did this customer actually pay?" had to read the database. This screen
/// answers that question, and is the only place a refund can be started, so refunds always go through the
/// service that keeps the ledger, the tax allocation, and the gateway in agreement.
/// </remarks>
[Admin("payments/{action}/{itemId?}", "Payments{action}")]
public sealed class PaymentsAdminController : Controller
{
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly IPaymentRefundStore _refundStore;
    private readonly ICheckoutRefundService _refundService;
    private readonly IEnumerable<ICheckoutPaymentProvider> _paymentProviders;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentsAdminController"/> class.
    /// </summary>
    /// <param name="attemptStore">The durable payment attempt ledger.</param>
    /// <param name="refundStore">The durable refund ledger.</param>
    /// <param name="refundService">The service that issues refunds.</param>
    /// <param name="paymentProviders">The registered payment providers, used to build the method filter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface outcomes.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentsAdminController(
        IPaymentAttemptStore attemptStore,
        IPaymentRefundStore refundStore,
        ICheckoutRefundService refundService,
        IEnumerable<ICheckoutPaymentProvider> paymentProviders,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<PaymentsAdminController> htmlLocalizer,
        IStringLocalizer<PaymentsAdminController> stringLocalizer)
    {
        _attemptStore = attemptStore;
        _refundStore = refundStore;
        _refundService = refundService;
        _paymentProviders = paymentProviders;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays the payments ledger.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("payments", "PaymentsIndex")]
    public async Task<IActionResult> Index(
        PaymentsAdminIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _attemptStore.PageAsync(pager.Page, pager.PageSize, new PaymentAttemptQuery
        {
            ProviderKey = options.ProviderKey,
            State = options.State,
            SessionId = options.SessionId,
        });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.ProviderKey))
        {
            routeData.Values.TryAdd("Options.ProviderKey", options.ProviderKey);
        }

        if (options.State.HasValue)
        {
            routeData.Values.TryAdd("Options.State", options.State.Value);
        }

        options.States = BuildStateItems(options.State);
        options.Providers = BuildProviderItems(options.ProviderKey);

        return View(new PaymentsAdminIndexViewModel
        {
            Options = options,
            Attempts = [.. result.Entries],
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            CanRefund = true,
        });
    }

    /// <summary>
    /// Preserves the ledger filter when the toolbar is submitted.
    /// </summary>
    /// <param name="options">The filter options.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("payments", "PaymentsIndex")]
    public async Task<IActionResult> IndexFilterPost(PaymentsAdminIndexOptions options)
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

        if (options.State.HasValue)
        {
            routeValues.TryAdd("Options.State", options.State.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Displays the refund form for a settled payment.
    /// </summary>
    /// <param name="itemId">The payment attempt identifier.</param>
    public async Task<IActionResult> Refund(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        var attempt = await FindRefundableAsync(itemId);

        if (attempt is null)
        {
            return NotFound();
        }

        var alreadyRefunded = await GetRefundedTotalAsync(attempt);
        var settled = attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount;

        return View(new RefundRequestViewModel
        {
            Attempt = attempt,
            AlreadyRefunded = alreadyRefunded,
            Amount = CurrencyScale.Round(Math.Max(0m, settled - alreadyRefunded), attempt.Currency),
        });
    }

    /// <summary>
    /// Issues the refund.
    /// </summary>
    /// <param name="itemId">The payment attempt identifier.</param>
    /// <param name="model">The submitted form.</param>
    [HttpPost]
    [ActionName(nameof(Refund))]
    public async Task<IActionResult> RefundPost(string itemId, RefundRequestViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageRefunds))
        {
            return Forbid();
        }

        var attempt = await FindRefundableAsync(itemId);

        if (attempt is null)
        {
            return NotFound();
        }

        var alreadyRefunded = await GetRefundedTotalAsync(attempt);
        var settled = attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount;
        var remaining = CurrencyScale.Round(Math.Max(0m, settled - alreadyRefunded), attempt.Currency);

        // The refund service enforces this too, but catching it here means the operator sees what is
        // actually left rather than a gateway error after the fact.
        if (model.Amount <= 0m)
        {
            ModelState.AddModelError(nameof(model.Amount), S["Enter an amount greater than zero."]);
        }
        else if (model.Amount > remaining)
        {
            ModelState.AddModelError(nameof(model.Amount), S["Only {0} {1} is still refundable on this payment.", CurrencyScale.Format(remaining, attempt.Currency), attempt.Currency]);
        }

        if (!ModelState.IsValid)
        {
            model.Attempt = attempt;
            model.AlreadyRefunded = alreadyRefunded;

            return View(model);
        }

        var refund = await _refundService.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = attempt.SessionId,
            OriginalTransactionId = attempt.TransactionId,
            Amount = model.Amount,
            Reason = model.Reason,
        });

        switch (refund.Status)
        {
            case RefundStatus.Succeeded:
                await _notifier.SuccessAsync(H["The refund was completed."]);
                break;

            case RefundStatus.Failed:
            case RefundStatus.Canceled:
                await _notifier.ErrorAsync(H["The refund failed: {0}", refund.FailureReason ?? S["The provider did not say why."].Value]);
                break;

            case RefundStatus.PendingManualReview:
                await _notifier.WarningAsync(H["This payment method cannot refund automatically. The refund was recorded for you to settle by hand."]);
                break;

            default:
                await _notifier.InformationAsync(H["The refund was submitted and is still being processed."]);
                break;
        }

        return RedirectToAction(nameof(Index));
    }

    // Only a settled payment with a real gateway transaction can be refunded. Offering the form for anything
    // else would let an operator start a refund that can only fail.
    private async Task<PaymentAttempt> FindRefundableAsync(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        var attempt = await _attemptStore.FindByIdAsync(itemId);

        if (attempt is null || attempt.State != PaymentAttemptState.Succeeded || string.IsNullOrEmpty(attempt.TransactionId))
        {
            return null;
        }

        return attempt;
    }

    // Everything already given back against this payment, whether it settled or is still in flight. Counting
    // an in-flight refund is deliberate: ignoring it would let an operator start a second refund for money
    // that is already on its way back.
    private async Task<decimal> GetRefundedTotalAsync(PaymentAttempt attempt)
    {
        var refunds = await _refundStore.GetByOriginalTransactionAsync(attempt.TransactionId);

        var total = refunds
            .Where(refund => refund.Status is not (RefundStatus.Failed or RefundStatus.Canceled))
            .Sum(refund => refund.RefundGrossAmount);

        return CurrencyScale.Round(total, attempt.Currency);
    }

    private List<SelectListItem> BuildStateItems(PaymentAttemptState? selected)
    {
        var items = new List<SelectListItem>
        {
            new() { Text = S["All states"], Value = string.Empty, Selected = !selected.HasValue },
        };

        foreach (var state in Enum.GetValues<PaymentAttemptState>())
        {
            items.Add(new SelectListItem
            {
                Text = state.ToString(),
                Value = state.ToString(),
                Selected = selected == state,
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
