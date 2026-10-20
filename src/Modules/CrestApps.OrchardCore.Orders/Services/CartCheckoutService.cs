using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Checks the current cart out: prices it again, turns it into an order that holds its stock, and starts the order's
/// checkout. The cart page's Checkout button and the commerce API both go through here.
/// </summary>
public sealed class CartCheckoutService
{
    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly ICheckoutEngine _engine;
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public CartCheckoutService(
        ICartService cartService,
        IOrderService orderService,
        ICheckoutEngine engine,
        UserManager<IUser> userManager,
        ISession session,
        IServiceProvider serviceProvider,
        IStringLocalizer<CartCheckoutService> stringLocalizer)
    {
        _cartService = cartService;
        _orderService = orderService;
        _engine = engine;
        _userManager = userManager;
        _session = session;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks out the cart of <paramref name="user"/> (or the guest the request belongs to).
    /// </summary>
    public async Task<CartCheckoutResult> StartAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        // Priced again here, not taken from a page: the cart may have changed elsewhere, and a price may have changed
        // since it was shown.
        var pricing = await _cartService.PriceAsync(await _cartService.GetCurrentAsync(cancellationToken), cancellationToken);

        if (!pricing.CanCheckout)
        {
            return pricing.Lines.Count == 0
                ? CartCheckoutResult.Fail(CartCheckoutFailure.EmptyCart, S["Your cart is empty."])
                : CartCheckoutResult.Fail(CartCheckoutFailure.CartProblems, S["Some items in your cart can't be bought as they are. Change or remove them to check out."]);
        }

        var created = await _orderService.CreateFromCartAsync(pricing, cancellationToken);

        // Another buyer may have just taken the last units.
        if (!created.Succeeded)
        {
            return CartCheckoutResult.Fail(CartCheckoutFailure.OutOfStock, [.. created.Problems]);
        }

        var order = created.Order;

        // A guest's details come from the checkout; a signed-in customer's are already known.
        if (user?.Identity?.IsAuthenticated == true && await _userManager.GetUserAsync(user) is User account)
        {
            order.CustomerName = account.UserName;
            order.CustomerEmail = account.Email;

            // With Customers enabled, the order belongs to the user's customer, made on their first order.
            if (_serviceProvider.GetService<ICustomerService>() is { } customers &&
                await customers.GetOrCreateForUserAsync(account.UserId, cancellationToken) is { } customer)
            {
                order.CustomerId = customer.ItemId;
                order.CustomerName = customer.GetName() ?? account.UserName;

                await customers.TouchAsync(customer, cancellationToken);
            }
        }

        // Committed here, with the stock the order holds, so a buyer who just took the last units elsewhere makes this
        // one fail cleanly instead of selling them twice.
        try
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();

            return CartCheckoutResult.Fail(CartCheckoutFailure.Concurrency, S["Someone else is buying the same items right now. Try checking out again."]);
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = order.ItemId,
        }, cancellationToken);


        return new CartCheckoutResult { Session = session, OrderId = order.ItemId };
    }
}

/// <summary>
/// What checking out a cart came to: a started checkout, or why there isn't one.
/// </summary>
public sealed class CartCheckoutResult
{
    public CheckoutSession Session { get; init; }

    public string OrderId { get; init; }

    public CartCheckoutFailure? Failure { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool Succeeded
        => Failure is null && Session is not null;

    internal static CartCheckoutResult Fail(CartCheckoutFailure failure, params string[] problems)
        => new() { Failure = failure, Problems = problems };
}

public enum CartCheckoutFailure
{
    EmptyCart,
    CartProblems,
    OutOfStock,
    Concurrency,
}
