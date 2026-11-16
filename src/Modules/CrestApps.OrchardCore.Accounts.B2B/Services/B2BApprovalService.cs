using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Approvals of account buyers' orders: an order over the buyer's limit, or one that takes the account past its monthly
/// budget, waits for an account admin to approve it before it's paid. An admin's own orders don't.
/// </summary>
/// <remarks>
/// The budget counts what the account's orders this month spent or committed: paid orders, orders waiting for approval
/// or approved and not paid yet, and orders being paid, which <see cref="B2BApprovalPaymentGate"/> marks with a
/// <see cref="Order.BudgetHold"/> under the account's lock (<see cref="GetBudgetLockKey"/>) as their payment begins.
/// </remarks>
public sealed class B2BApprovalService
{
    /// <summary>
    /// The kind of the email that asks an account's admins to approve an order.
    /// </summary>
    public const string ApprovalRequestedEmailKind = "b2b-approval-requested";

    /// <summary>
    /// The kind of the email that tells the buyer an admin approved or rejected their order.
    /// </summary>
    public const string ApprovalDecidedEmailKind = "b2b-approval-decided";

    private const string BudgetLockPrefix = "B2B_BUDGET_";

    private static readonly OrderStatus[] _committedStatuses = [OrderStatus.Paid, OrderStatus.AwaitingPayment, OrderStatus.Draft];

    private readonly IOrderStore _orders;
    private readonly IOrderService _orderService;
    private readonly ISiteService _siteService;
    private readonly ICommerceEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly IShopperCurrency _shopperCurrency;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public B2BApprovalService(
        IOrderStore orders,
        IOrderService orderService,
        ISiteService siteService,
        ICommerceEmailSender emailSender,
        IClock clock,
        IStringLocalizer<B2BApprovalService> stringLocalizer,
        IShopperCurrency shopperCurrency,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _orders = orders;
        _orderService = orderService;
        _siteService = siteService;
        _emailSender = emailSender;
        _clock = clock;
        _shopperCurrency = shopperCurrency;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns why a buyer's order of <paramref name="total"/> needs an account admin's approval, or
    /// <see langword="null"/> when it doesn't.
    /// </summary>
    public async Task<string> GetReasonAsync(B2BAccount account, AccountBuyer buyer, decimal total, string currency, string excludeOrderId = null, CancellationToken cancellationToken = default)
    {
        if (account is null || buyer is null || buyer.Role == AccountMemberRole.Admin || (buyer.OrderLimit is null && account.MonthlyBudget is null))
        {
            return null;
        }

        var storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        var amount = await ToStoreCurrencyAsync(total, currency, storeCurrency, cancellationToken);

        if (buyer.OrderLimit is { } limit && amount > limit)
        {
            return S["it's over your order limit of {0}", ProductPriceFormatter.Format(limit, storeCurrency)];
        }

        if (account.MonthlyBudget is { } budget)
        {
            var spent = await GetSpentThisMonthAsync(account, storeCurrency, excludeOrderId, cancellationToken);

            if (spent + amount > budget)
            {
                return S["it takes {0} past its monthly budget of {1}, with {2} spent this month", account.Name, ProductPriceFormatter.Format(budget, storeCurrency), ProductPriceFormatter.Format(spent, storeCurrency)];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the key of the lock an account's budget is checked and held under, so its buyers' payments take turns.
    /// </summary>
    public static string GetBudgetLockKey(string accountId)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountId);

        return BudgetLockPrefix + accountId;
    }

    /// <summary>
    /// Returns what the account's orders this calendar month spent or committed, in the store's currency: paid orders,
    /// orders waiting for approval or approved but not paid, and orders whose payment began (their budget hold). An open
    /// checkout that committed nothing doesn't count; the abandoned order sweep cancels it.
    /// </summary>
    public async Task<decimal> GetSpentThisMonthAsync(B2BAccount account, string storeCurrency, string excludeOrderId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var now = _clock.UtcNow;
        var (_, orders) = await _orders.PageAsync(new OrderQuery
        {
            AccountId = account.AccountId,
            Statuses = _committedStatuses,
            CreatedOnOrAfter = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        }, 0, 1000, cancellationToken);

        var spent = 0m;

        foreach (var order in orders.Where(order => order.ItemId != excludeOrderId))
        {
            if (GetCommitted(order) is { } committed)
            {
                spent += await ToStoreCurrencyAsync(committed.Amount, committed.Currency, storeCurrency, cancellationToken);
            }
        }

        return spent;
    }

    /// <summary>
    /// Holds the order's total against its account's budget as its payment begins, until it's paid or canceled. The
    /// caller holds the account's budget lock and commits before letting it go.
    /// </summary>
    public async Task HoldBudgetAsync(Order order, decimal total, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        order.BudgetHold = new OrderBudgetHold
        {
            Amount = total,
            Currency = currency,
            HeldUtc = _clock.UtcNow,
        };

        await _orders.UpdateAsync(order, cancellationToken);
    }

    // What an order of the month takes from the budget: a paid order its total, an unpaid one what an admin is asked to
    // approve (or approved) or what its payment holds, and an open checkout nothing.
    private static (decimal Amount, string Currency)? GetCommitted(Order order)
    {
        if (order.Status == OrderStatus.Paid)
        {
            return (order.Total ?? order.Subtotal, order.Currency);
        }

        if (order.Status is not (OrderStatus.AwaitingPayment or OrderStatus.Draft))
        {
            return null;
        }

        // What an admin is asked to approve, or approved, covers what its payment can hold.
        if (order.Approval is { Status: OrderApprovalStatus.Pending or OrderApprovalStatus.Approved } approval)
        {
            return (approval.Amount, approval.Currency ?? order.Currency);
        }

        return order.BudgetHold is { } hold
            ? (hold.Amount, hold.Currency ?? order.Currency)
            : null;
    }

    /// <summary>
    /// Asks the account's admins to approve the order: it waits, kept as an order to pay, and they're emailed.
    /// </summary>
    public async Task RequestAsync(B2BAccount account, Order order, string reason, decimal total, string currency, string buyerName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(order);

        order.Approval = new OrderApproval
        {
            Status = OrderApprovalStatus.Pending,
            Reason = reason,
            Amount = total,
            Currency = currency,
            RequestedUtc = _clock.UtcNow,
            RequestedBy = buyerName,
        };

        // An order to pay, so it's kept when its checkout ends, and paid from the buyer's orders once approved.
        order.Invoice ??= new OrderInvoice { CreatedBy = buyerName };
        order.AccountId ??= account.AccountId;
        order.ModifiedUtc = _clock.UtcNow;

        await _orderService.AddNoteAsync(order, S["{0} asked {1} to approve the order: {2}.", buyerName, account.Name, reason], OrderNoteKind.Event, buyerName, cancellationToken);
        await _orders.UpdateAsync(order, cancellationToken);
        await EmailAsync(
            ApprovalRequestedEmailKind,
            order,
            account.Buyers.Where(buyer => buyer.Role == AccountMemberRole.Admin).Select(buyer => buyer.Email),
            S["Order {0} needs your approval", order.OrderNumber],
            S["{0} placed order {1} for {2}, which needs your approval: {3}. Approve or reject it on your account page.", buyerName, order.OrderNumber, ProductPriceFormatter.Format(total, currency), reason],
            "BusinessAccount",
            cancellationToken);
    }

    /// <summary>
    /// Records an account admin's decision. A rejected order is canceled. Returns whether the order was waiting.
    /// </summary>
    public async Task<bool> DecideAsync(Order order, bool approve, string adminName, string note, CancellationToken cancellationToken = default)
    {
        if (order?.Approval is not { Status: OrderApprovalStatus.Pending } approval || order.Status is not (OrderStatus.Draft or OrderStatus.AwaitingPayment))
        {
            return false;
        }

        approval.Status = approve ? OrderApprovalStatus.Approved : OrderApprovalStatus.Rejected;
        approval.DecidedUtc = _clock.UtcNow;
        approval.DecidedBy = adminName;
        approval.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 1000)];

        await _orderService.AddNoteAsync(order, approve ? S["{0} approved the order.", adminName] : S["{0} rejected the order.", adminName], OrderNoteKind.Event, adminName, cancellationToken);

        if (approve)
        {
            order.ModifiedUtc = _clock.UtcNow;
            await _orders.UpdateAsync(order, cancellationToken);
        }
        else
        {
            await _orderService.CancelAsync(order, OrderCancelReason.Rejected, cancellationToken);
        }

        await EmailAsync(
            ApprovalDecidedEmailKind,
            order,
            [order.CustomerEmail],
            approve ? S["Order {0} was approved", order.OrderNumber] : S["Order {0} was rejected", order.OrderNumber],
            approve
                ? S["{0} approved order {1}. Pay it from your orders to place it.", adminName, order.OrderNumber]
                : S["{0} rejected order {1}. {2}", adminName, order.OrderNumber, approval.Note ?? string.Empty],
            "AccountOrders",
            cancellationToken);

        return true;
    }

    private async Task<decimal> ToStoreCurrencyAsync(decimal amount, string currency, string storeCurrency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currency) || string.IsNullOrEmpty(storeCurrency) || string.Equals(currency, storeCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        return await _shopperCurrency.ConvertAsync(amount, currency, storeCurrency, isPrice: false, cancellationToken) is { } converted
            ? converted
            : amount;
    }

    private async Task EmailAsync(string kind, Order order, IEnumerable<string> recipients, string subject, string text, string routeName, CancellationToken cancellationToken)
    {
        var to = string.Join(",", recipients.Where(email => !string.IsNullOrWhiteSpace(email)).Distinct(StringComparer.OrdinalIgnoreCase));

        if (to.Length == 0 || !_emailSender.CanSend)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var url = httpContext is null ? null : _linkGenerator.GetUriByRouteValues(httpContext, routeName, values: null);

        await _emailSender.SendAsync(new CommerceEmail
        {
            Kind = kind,
            To = to,
            Models = OrderEmailModels.For(order),
            Reference = order.OrderNumber,
        }, _ =>
        {
            var body = new StringBuilder(text);
            var encoder = HtmlEncoder.Default;

            if (!string.IsNullOrEmpty(url))
            {
                body.AppendLine().AppendLine().Append(url);
            }

            return Task.FromResult(new CommerceEmailContent
            {
                Subject = subject,
                Text = body.ToString(),
                Html = "<p>" + encoder.Encode(text) + "</p>" + (string.IsNullOrEmpty(url) ? string.Empty : "<p><a href=\"" + encoder.Encode(url) + "\">" + encoder.Encode(url) + "</a></p>"),
                Values = new Dictionary<string, string> { ["Url"] = url ?? string.Empty },
            });
        }, cancellationToken);
    }
}
