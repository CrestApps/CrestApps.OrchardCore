using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// A signed-in buyer's saved cards.
/// </summary>
public interface IStripeWalletService
{
    /// <summary>
    /// Returns the Stripe customer holding the user's saved cards, or <see langword="null"/> when they have none.
    /// </summary>
    Task<string> GetCustomerIdAsync(string userId);

    /// <summary>
    /// Returns the Stripe customer holding the user's saved cards, making one the first time.
    /// </summary>
    Task<string> EnsureCustomerAsync(string userId, string name, string email);

    /// <summary>
    /// Lists the user's saved cards, newest first.
    /// </summary>
    Task<IReadOnlyList<StripeSavedCard>> ListCardsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user's saved card with the id, or <see langword="null"/> when it isn't theirs.
    /// </summary>
    Task<StripeSavedCard> FindCardAsync(string userId, string paymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one of the user's saved cards; a card that isn't theirs is left alone.
    /// </summary>
    Task<bool> RemoveCardAsync(string userId, string paymentMethodId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A signed-in buyer's saved cards: the one Stripe customer that holds them, kept on their user, and the cards
/// themselves, read from Stripe each time so the list is never out of date.
/// </summary>
/// <remarks>
/// A card is only ever used or removed after checking that it belongs to the buyer's own customer, so a card id
/// sent from a browser can't spend or remove someone else's card.
/// </remarks>
public sealed class StripeWalletService : IStripeWalletService
{
    /// <summary>
    /// The metadata key on a Stripe customer made for a user's saved cards.
    /// </summary>
    public const string WalletUserMetadataKey = "wallet_user_id";

    private const string UserPropertyName = "StripeWallet";

    private readonly UserManager<IUser> _userManager;
    private readonly StripeClient _stripeClient;
    private readonly ILogger _logger;

    public StripeWalletService(UserManager<IUser> userManager, StripeClient stripeClient, ILogger<StripeWalletService> logger)
    {
        _userManager = userManager;
        _stripeClient = stripeClient;
        _logger = logger;
    }

    /// <summary>
    /// Returns the Stripe customer holding the user's saved cards, or <see langword="null"/> when they have none.
    /// </summary>
    public async Task<string> GetCustomerIdAsync(string userId)
        => await FindUserAsync(userId) is { } user ? ReadCustomerId(user) : null;

    /// <summary>
    /// Returns the Stripe customer holding the user's saved cards, making one the first time.
    /// </summary>
    public async Task<string> EnsureCustomerAsync(string userId, string name, string email)
    {
        if (await FindUserAsync(userId) is not User user)
        {
            return null;
        }

        if (ReadCustomerId(user) is { Length: > 0 } existing)
        {
            return existing;
        }

        var customer = await new CustomerService(_stripeClient).CreateAsync(new CustomerCreateOptions
        {
            Name = name,
            Email = email ?? user.Email,
            Metadata = new Dictionary<string, string> { [WalletUserMetadataKey] = userId },
        }, new RequestOptions { IdempotencyKey = "wallet_" + userId });

        user.Properties[UserPropertyName] = new System.Text.Json.Nodes.JsonObject { ["CustomerId"] = customer.Id };
        await _userManager.UpdateAsync(user);

        return customer.Id;
    }

    /// <summary>
    /// Lists the user's saved cards, newest first.
    /// </summary>
    public async Task<IReadOnlyList<StripeSavedCard>> ListCardsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (await GetCustomerIdAsync(userId) is not { Length: > 0 } customerId)
        {
            return [];
        }

        try
        {
            var methods = await new PaymentMethodService(_stripeClient).ListAsync(new PaymentMethodListOptions
            {
                Customer = customerId,
                Type = "card",
                Limit = 20,
            }, cancellationToken: cancellationToken);

            return [.. methods.Data
                .Where(method => method.Card is not null)
                .OrderByDescending(method => method.Created)
                .Select(method => new StripeSavedCard
                {
                    Id = method.Id,
                    Brand = method.Card.DisplayBrand ?? method.Card.Brand,
                    Last4 = method.Card.Last4,
                    ExpMonth = (int)method.Card.ExpMonth,
                    ExpYear = (int)method.Card.ExpYear,
                })];
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "The saved cards of a user couldn't be read from Stripe.");

            return [];
        }
    }

    /// <summary>
    /// Returns the user's saved card with the id, or <see langword="null"/> when it isn't theirs.
    /// </summary>
    public async Task<StripeSavedCard> FindCardAsync(string userId, string paymentMethodId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(paymentMethodId)
            ? null
            : (await ListCardsAsync(userId, cancellationToken)).FirstOrDefault(card => string.Equals(card.Id, paymentMethodId, StringComparison.Ordinal));

    /// <summary>
    /// Removes one of the user's saved cards; a card that isn't theirs is left alone.
    /// </summary>
    public async Task<bool> RemoveCardAsync(string userId, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        if (await FindCardAsync(userId, paymentMethodId, cancellationToken) is null)
        {
            return false;
        }

        await new PaymentMethodService(_stripeClient).DetachAsync(paymentMethodId, cancellationToken: cancellationToken);

        return true;
    }

    private async Task<IUser> FindUserAsync(string userId)
        => string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);

    private static string ReadCustomerId(IUser user)
        => user is User { Properties: { } properties } &&
            properties[UserPropertyName] is System.Text.Json.Nodes.JsonObject wallet &&
            wallet["CustomerId"]?.GetValue<string>() is { Length: > 0 } customerId
                ? customerId
                : null;
}

/// <summary>
/// A card a buyer saved, as their payment methods page and the checkout show it.
/// </summary>
public sealed class StripeSavedCard
{
    public string Id { get; set; }

    public string Brand { get; set; }

    public string Last4 { get; set; }

    public int ExpMonth { get; set; }

    public int ExpYear { get; set; }
}
