using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Carts.Handlers;

/// <summary>
/// Keeps what a visitor put in their cart when they sign in: the guest cart joins the user's cart.
/// </summary>
public sealed class CartLoginFormEvent : LoginFormEventBase
{
    private readonly ICartService _cartService;
    private readonly ILogger _logger;

    public CartLoginFormEvent(
        ICartService cartService,
        ILogger<CartLoginFormEvent> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    public override async Task LoggedInAsync(IUser user)
    {
        if (user is not User { UserId: { Length: > 0 } userId })
        {
            return;
        }

        try
        {
            await _cartService.MergeGuestCartAsync(userId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Signing in matters more than the cart: a cart that could not be merged stays the guest's.
            _logger.LogError(exception, "The guest cart could not be merged into the cart of user '{UserId}'.", userId);
        }
    }
}
