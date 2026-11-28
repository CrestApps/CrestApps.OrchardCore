using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// A gift card AI tool: it needs <see cref="OrderPermissions.ManageGiftCards"/>, the permission of Commerce › Gift cards.
/// A card's code is a bearer credential worth money, so no tool returns it or takes it: cards are shown by the last
/// characters of their code only.
/// </summary>
internal abstract class GiftCardAIToolBase : OrdersAIToolBase
{
    /// <summary>
    /// The longest idempotency key a money tool takes.
    /// </summary>
    protected const int MaxKeyLength = 128;

    /// <summary>
    /// How long an idempotency key is kept.
    /// </summary>
    protected static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(24);

    /// <inheritdoc/>
    protected override Permission Permission => OrderPermissions.ManageGiftCards;

    /// <summary>
    /// Finds a gift card by the <c>giftCardId</c> argument.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="giftCardId">The card's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The card, or the sentence that says why there is none.</returns>
    protected static async Task<(GiftCard Card, string Error)> FindCardAsync(IServiceProvider services, string giftCardId, CancellationToken cancellationToken)
    {
        if (giftCardId is null)
        {
            return (null, "Nothing was done: give the gift card's id. Use listGiftCards to find it, by the last characters of its code or the recipient's email.");
        }

        return await services.GetRequiredService<IGiftCardStore>().FindByIdAsync(giftCardId, cancellationToken) is { } card
            ? (card, null)
            : (null, $"Nothing was done: no gift card has the id '{giftCardId}'. Use listGiftCards to find it.");
    }

    /// <summary>
    /// Gets a card's status as the gift cards list filters it: disabled, expired, used up or active.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>The status name.</returns>
    protected static string StatusOf(GiftCard card, DateTime utcNow)
    {
        if (!card.IsEnabled)
        {
            return "disabled";
        }

        if (card.ExpiresUtc.HasValue && card.ExpiresUtc.Value <= utcNow)
        {
            return "expired";
        }

        return card.Balance <= 0m ? "usedUp" : "active";
    }

    /// <summary>
    /// Gets the current UTC time from the site's clock (<see cref="IClock"/>).
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The current UTC time.</returns>
    protected static DateTime UtcNow(IServiceProvider services)
        => services.GetRequiredService<IClock>().UtcNow;

    /// <summary>
    /// Builds the allow-listed fields of a card, with the code masked to its last characters.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="card">The card.</param>
    /// <returns>The fields.</returns>
    protected static async Task<Dictionary<string, object>> DescribeAsync(IServiceProvider services, GiftCard card)
        => new()
        {
            ["id"] = card.ItemId,
            ["codeEndsWith"] = card.LastCharacters,
            ["balance"] = card.Balance,
            ["initialValue"] = card.InitialValue,
            ["currency"] = card.Currency,
            ["status"] = StatusOf(card, UtcNow(services)),
            ["isEnabled"] = card.IsEnabled,
            ["expiresAt"] = await LocalTimeAsync(services, card.ExpiresUtc),
            ["recipientName"] = card.RecipientName,
            ["recipientEmail"] = card.RecipientEmail,
            ["orderId"] = card.OrderId,
            ["orderNumber"] = card.OrderNumber,
            ["createdAt"] = await LocalTimeAsync(services, card.CreatedUtc),
        };

    /// <summary>
    /// Commits a change of a card at once. When a checkout spent the card at the same moment, nothing is kept.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when it was saved, or the sentence that says why not.</returns>
    protected static async Task<string> CommitCardAsync(IServiceProvider services, CancellationToken cancellationToken)
        => await CommitAsync(services, cancellationToken) is null
            ? null
            : "Nothing was saved: the gift card changed at the same moment, such as at a checkout. Read it again with getGiftCard and retry in your next message.";

    /// <summary>
    /// Reads an idempotency key argument.
    /// </summary>
    /// <param name="value">The key as given.</param>
    /// <returns>The key, or <see langword="null"/> when it is missing or too long.</returns>
    protected static string CheckKey(string value)
        => value is { Length: > 0 and <= MaxKeyLength } ? value : null;

    /// <summary>
    /// Gets the scope an idempotency key belongs to: the site, the tool and the caller.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="toolName">The tool's name.</param>
    /// <param name="caller">The caller.</param>
    /// <returns>The scope.</returns>
    protected static string KeyScope(IServiceProvider services, string toolName, ClaimsPrincipal caller)
        => $"{services.GetService<ShellSettings>()?.Name}|ai-tool:{toolName}|{caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name}";

    /// <summary>
    /// Writes an amount for a fingerprint.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The invariant text.</returns>
    protected static string Invariant(decimal amount)
        => amount.ToString(CultureInfo.InvariantCulture);
}
