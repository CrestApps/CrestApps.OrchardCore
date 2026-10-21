using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// Approvals of company buyers' orders: an order over the buyer's limit, or one that takes the company past its monthly
/// budget, waits for a company admin to approve it before it's paid. An admin's own orders don't.
/// </summary>
public sealed class CompanyApprovalService
{
    private readonly IOrderStore _orders;
    private readonly IOrderService _orderService;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IShopperCurrency _shopperCurrency;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public CompanyApprovalService(
        IOrderStore orders,
        IOrderService orderService,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<CompanyApprovalService> logger,
        IStringLocalizer<CompanyApprovalService> stringLocalizer,
        IShopperCurrency shopperCurrency,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _orders = orders;
        _orderService = orderService;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        _shopperCurrency = shopperCurrency;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns why a buyer's order of <paramref name="total"/> needs a company admin's approval, or
    /// <see langword="null"/> when it doesn't.
    /// </summary>
    public async Task<string> GetReasonAsync(Company company, CompanyBuyer buyer, decimal total, string currency, string excludeOrderId = null, CancellationToken cancellationToken = default)
    {
        if (company is null || buyer is null || buyer.Role == CompanyBuyerRole.Admin || (buyer.OrderLimit is null && company.MonthlyBudget is null))
        {
            return null;
        }

        var storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        var amount = await ToStoreCurrencyAsync(total, currency, storeCurrency, cancellationToken);

        if (buyer.OrderLimit is { } limit && amount > limit)
        {
            return S["it's over your order limit of {0}", ProductPriceFormatter.Format(limit, storeCurrency)];
        }

        if (company.MonthlyBudget is { } budget)
        {
            var spent = await GetSpentThisMonthAsync(company, storeCurrency, excludeOrderId, cancellationToken);

            if (spent + amount > budget)
            {
                return S["it takes {0} past its monthly budget of {1}, with {2} spent this month", company.Name, ProductPriceFormatter.Format(budget, storeCurrency), ProductPriceFormatter.Format(spent, storeCurrency)];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns what the company's paid orders came to this calendar month, in the store's currency.
    /// </summary>
    public async Task<decimal> GetSpentThisMonthAsync(Company company, string storeCurrency, string excludeOrderId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);

        var now = _clock.UtcNow;
        var (_, orders) = await _orders.PageAsync(new OrderQuery
        {
            CompanyId = company.ItemId,
            Statuses = [OrderStatus.Paid],
            CreatedOnOrAfter = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        }, 0, 1000, cancellationToken);

        var spent = 0m;

        foreach (var order in orders.Where(order => order.ItemId != excludeOrderId))
        {
            spent += await ToStoreCurrencyAsync(order.Total ?? order.Subtotal, order.Currency, storeCurrency, cancellationToken);
        }

        return spent;
    }

    /// <summary>
    /// Asks the company's admins to approve the order: it waits, kept as an order to pay, and they're emailed.
    /// </summary>
    public async Task RequestAsync(Company company, Order order, string reason, decimal total, string currency, string buyerName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);
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
        order.CompanyId ??= company.ItemId;
        order.ModifiedUtc = _clock.UtcNow;

        await _orderService.AddNoteAsync(order, S["{0} asked {1} to approve the order: {2}.", buyerName, company.Name, reason], OrderNoteKind.Event, buyerName, cancellationToken);
        await _orders.UpdateAsync(order, cancellationToken);
        await EmailAsync(
            company.Buyers.Where(buyer => buyer.Role == CompanyBuyerRole.Admin).Select(buyer => buyer.Email),
            S["Order {0} needs your approval", order.OrderNumber],
            S["{0} placed order {1} for {2}, which needs your approval: {3}. Approve or reject it on your company page.", buyerName, order.OrderNumber, ProductPriceFormatter.Format(total, currency), reason],
            "AccountCompany",
            cancellationToken);
    }

    /// <summary>
    /// Records a company admin's decision. A rejected order is canceled. Returns whether the order was waiting.
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

    private async Task EmailAsync(IEnumerable<string> recipients, string subject, string text, string routeName, CancellationToken cancellationToken)
    {
        var to = string.Join(",", recipients.Where(email => !string.IsNullOrWhiteSpace(email)).Distinct(StringComparer.OrdinalIgnoreCase));

        if (to.Length == 0 || _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var url = httpContext is null ? null : _linkGenerator.GetUriByRouteValues(httpContext, routeName, values: null);
        var body = new StringBuilder(text);
        var encoder = HtmlEncoder.Default;

        if (!string.IsNullOrEmpty(url))
        {
            body.AppendLine().AppendLine().Append(url);
        }

        var html = "<p>" + encoder.Encode(text) + "</p>" + (string.IsNullOrEmpty(url) ? string.Empty : "<p><a href=\"" + encoder.Encode(url) + "\">" + encoder.Encode(url) + "</a></p>");
        var result = await emailService.SendAsync(new MailMessage { To = to, Subject = subject, TextBody = body.ToString(), HtmlBody = html }, cancellationToken: cancellationToken);

        if (!result.Succeeded && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("An approval email about '{Subject}' couldn't be sent.", subject);
        }
    }
}
