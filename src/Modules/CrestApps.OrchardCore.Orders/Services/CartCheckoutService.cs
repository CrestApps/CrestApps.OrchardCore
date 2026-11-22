using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Throttling;
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
    private readonly IAttemptLimiter _limiter;

    internal readonly IStringLocalizer S;

    public CartCheckoutService(
        ICartService cartService,
        IOrderService orderService,
        ICheckoutEngine engine,
        UserManager<IUser> userManager,
        ISession session,
        IServiceProvider serviceProvider,
        IAttemptLimiter limiter,
        IStringLocalizer<CartCheckoutService> stringLocalizer)
    {
        _cartService = cartService;
        _orderService = orderService;
        _engine = engine;
        _userManager = userManager;
        _session = session;
        _serviceProvider = serviceProvider;
        _limiter = limiter;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks out the cart of <paramref name="user"/> (or the guest the request belongs to).
    /// </summary>
    /// <param name="user">The buyer.</param>
    /// <param name="clientNetworkKey">The network the request comes from (<c>ClientNetworkKey.NetworkOnly</c>).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<CartCheckoutResult> StartAsync(ClaimsPrincipal user, string clientNetworkKey, CancellationToken cancellationToken = default)
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

        // Placing the order holds its stock, so a network may start only so many checkouts, as every other checkout start
        // counts, whether or not the Rate Limits feature is on: guest carts can't hold the whole stock.
        if (await _limiter.AcquireAsync(CheckoutConstants.AttemptPolicies.CheckoutStartNetwork, clientNetworkKey, cancellationToken) is { Allowed: false } refused)
        {
            return CartCheckoutResult.Fail(CartCheckoutFailure.TooManyRequests, refused.RetryAfter, S["Too many checkouts were started from your network. Please wait a moment and try again."]);
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

    /// <summary>
    /// Gets when the buyer may try again, for <see cref="CartCheckoutFailure.TooManyRequests"/>.
    /// </summary>
    public TimeSpan RetryAfter { get; init; }

    public bool Succeeded
        => Failure is null && Session is not null;

    internal static CartCheckoutResult Fail(CartCheckoutFailure failure, params string[] problems)
        => new() { Failure = failure, Problems = problems };

    internal static CartCheckoutResult Fail(CartCheckoutFailure failure, TimeSpan retryAfter, string problem)
        => new() { Failure = failure, Problems = [problem], RetryAfter = retryAfter };
}

public enum CartCheckoutFailure
{
    EmptyCart,
    CartProblems,
    OutOfStock,
    Concurrency,
    TooManyRequests,
}
