using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Quotes: a signed-in buyer asks for one from their cart, and gets a draft order the store prices and sends; they
/// accept it by paying, or turn it down.
/// </summary>
public sealed class QuoteService
{
    public const int MaxMessageLength = 2000;

    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly IOrderStore _orderStore;
    private readonly UserManager<IUser> _userManager;
    private readonly ISiteService _siteService;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public QuoteService(
        ICartService cartService,
        IOrderService orderService,
        IOrderStore orderStore,
        UserManager<IUser> userManager,
        ISiteService siteService,
        ISession session,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<QuoteService> stringLocalizer)
    {
        _cartService = cartService;
        _orderService = orderService;
        _orderStore = orderStore;
        _userManager = userManager;
        _siteService = siteService;
        _session = session;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns whether an order can be paid as far as its quote goes: it has none, or the store sent it and it hasn't
    /// expired.
    /// </summary>
    public static bool CanPay(Order order, DateTime utcNow)
        => order?.Quote is not { } quote || quote.Status == OrderQuoteStatus.Accepted || quote.CanAccept(utcNow);

    /// <summary>
    /// Returns whether the buyer can still turn the order's quote down: asked for or sent, and not being paid.
    /// </summary>
    public static bool CanDecline(Order order)
        => order?.Quote is { Status: OrderQuoteStatus.Requested or OrderQuoteStatus.Sent } && order.Status == OrderStatus.Draft;

    /// <summary>
    /// Returns whether a user may ask for quotes: the store takes requests, and they're signed in.
    /// </summary>
    public async Task<bool> CanRequestAsync(ClaimsPrincipal user)
        => user?.Identity?.IsAuthenticated == true && (await _siteService.GetSettingsAsync<OrderSettings>()).AllowQuoteRequests;

    /// <summary>
    /// Turns the signed-in buyer's cart into a quote request: a draft order of its lines with the buyer's message and
    /// purchase order number, for the store to price and send. The cart is emptied.
    /// </summary>
    public async Task<(Order Order, IReadOnlyList<string> Problems)> RequestAsync(ClaimsPrincipal user, string message, string purchaseOrderNumber, CancellationToken cancellationToken = default)
    {
        if (!await CanRequestAsync(user) || await _userManager.GetUserAsync(user) is not User account)
        {
            return (null, [S["Sign in to ask for a quote."]]);
        }

        if (message?.Length > MaxMessageLength || purchaseOrderNumber?.Trim().Length > 64)
        {
            return (null, [S["The message is at most {0} characters, and the purchase order number 64.", MaxMessageLength]]);
        }

        var cart = await _cartService.GetCurrentAsync(cancellationToken);
        var pricing = await _cartService.PriceAsync(cart, cancellationToken);

        if (pricing.Lines.Count == 0)
        {
            return (null, [S["Your cart is empty."]]);
        }

        if (!pricing.CanCheckout)
        {
            return (null, [S["Some items in your cart can't be bought as they are. Change or remove them first."]]);
        }

        var now = _clock.UtcNow;
        var created = await _orderService.CreateFromCartAsync(pricing, order =>
        {
            order.Channel = OrderChannels.Draft;

            // A quote isn't the cart's checkout: a later checkout of the cart mustn't replace it.
            order.CartId = null;
            order.Invoice = new OrderInvoice { CreatedBy = account.UserName };
            order.Quote = new OrderQuote
            {
                RequestedUtc = now,
                Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
                Status = OrderQuoteStatus.Requested,
            };
            order.PurchaseOrderNumber = string.IsNullOrWhiteSpace(purchaseOrderNumber) ? null : purchaseOrderNumber.Trim();
        }, cancellationToken);

        if (!created.Succeeded)
        {
            return (null, created.Problems);
        }

        var quote = created.Order;

        quote.CustomerName = account.UserName;
        quote.CustomerEmail = account.Email;

        if (_serviceProvider.GetService<ICustomerService>() is { } customers &&
            await customers.GetOrCreateForUserAsync(account.UserId, cancellationToken) is { } customer)
        {
            quote.CustomerId = customer.ItemId;
            quote.CustomerName = customer.GetName() ?? account.UserName;
        }

        await _orderService.AddNoteAsync(quote, S["The buyer asked for a quote."], OrderNoteKind.Event, account.UserName, cancellationToken);
        await _orderStore.UpdateAsync(quote, cancellationToken);
        await _cartService.DeleteAsync(cart, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return (quote, []);
    }

    /// <summary>
    /// Turns down a quote the store sent: the order is canceled. Returns whether it was.
    /// </summary>
    public async Task<bool> DeclineAsync(Order order, string actorName, CancellationToken cancellationToken = default)
    {
        if (!CanDecline(order))
        {
            return false;
        }

        var quote = order.Quote;

        quote.Status = OrderQuoteStatus.Declined;
        quote.RespondedUtc = _clock.UtcNow;

        await _orderService.AddNoteAsync(order, S["The buyer turned the quote down."], OrderNoteKind.Event, actorName, cancellationToken);

        return await _orderService.CancelAsync(order, OrderCancelReason.Declined, cancellationToken);
    }
}
