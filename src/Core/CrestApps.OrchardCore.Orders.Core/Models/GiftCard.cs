using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// A gift card: a code with money on it that pays toward checkouts until it's spent.
/// </summary>
/// <remarks>
/// Its <see cref="Balance"/> only changes under the card's lock, through the checkout's tender ledger or an
/// adjustment, and every change is written to <see cref="Activity"/>, so what's on the card can always be explained.
/// </remarks>
public sealed class GiftCard : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the code, as printed: groups of four characters separated by dashes.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets what was put on the card when it was made.
    /// </summary>
    public decimal InitialValue { get; set; }

    /// <summary>
    /// Gets or sets what is left on it.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency the card holds. It pays only toward checkouts in that currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets whether it can be used.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time it stops working, or <see langword="null"/> for never.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the person it's for.
    /// </summary>
    public string RecipientName { get; set; }

    /// <summary>
    /// Gets or sets the email it was sent to.
    /// </summary>
    public string RecipientEmail { get; set; }

    /// <summary>
    /// Gets or sets a note for the store, never shown to the buyer.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the order that bought it, when it was bought.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the number of the order that bought it.
    /// </summary>
    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets every change to its balance, oldest first.
    /// </summary>
    public List<GiftCardActivity> Activity { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets the last four characters of the code, which is all a buyer is shown once it's applied.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string LastCharacters
        => GiftCardCodes.Normalize(Code) is { Length: >= 4 } normalized ? normalized[^4..] : Code;

    /// <summary>
    /// Changes the balance by <paramref name="amount"/>, never below zero, and records why.
    /// </summary>
    public void ChangeBalance(decimal amount, string reason, DateTime utcNow, string sessionId = null, string actor = null)
    {
        var before = Balance;

        Balance = Payments.Money.Round(Math.Max(0m, Balance + amount), Currency);
        Activity ??= [];
        Activity.Add(new GiftCardActivity
        {
            CreatedUtc = utcNow,
            Amount = Balance - before,
            Balance = Balance,
            Reason = reason,
            SessionId = sessionId,
            Actor = actor,
        });
    }

    /// <summary>
    /// Returns whether the card can pay right now, in <paramref name="currency"/> when it's given.
    /// </summary>
    public bool CanPay(DateTime utcNow, string currency = null)
        => IsEnabled &&
            Balance > 0m &&
            (!ExpiresUtc.HasValue || utcNow < ExpiresUtc.Value) &&
            (string.IsNullOrEmpty(currency) || string.Equals(Currency, currency, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One change to a gift card's balance.
/// </summary>
public sealed class GiftCardActivity
{
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how much it changed: positive when money was put on, negative when it was spent.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the balance afterwards.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Gets or sets why, such as "Spent at checkout" or a staff member's reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the checkout it was spent or refunded on, when it was.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets who made the change, for a change staff made.
    /// </summary>
    public string Actor { get; set; }
}

/// <summary>
/// Makes and reads gift card codes.
/// </summary>
public static class GiftCardCodes
{
    /// <summary>
    /// The number of characters in a code, without its dashes.
    /// </summary>
    public const int Length = 16;

    // No 0, O, 1 or I, which people misread on a card.
    private static readonly char[] _alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    /// <summary>
    /// Returns a new random code, in groups of four.
    /// </summary>
    public static string Generate()
        => Format(new string(System.Security.Cryptography.RandomNumberGenerator.GetItems<char>(_alphabet, Length)));

    /// <summary>
    /// Returns a code in groups of four separated by dashes.
    /// </summary>
    public static string Format(string code)
    {
        var normalized = Normalize(code);

        return string.IsNullOrEmpty(normalized) ? normalized : string.Join('-', normalized.Chunk(4).Select(group => new string(group)));
    }

    /// <summary>
    /// Returns a code in capitals without spaces or dashes, the way it's looked up.
    /// </summary>
    public static string Normalize(string code)
        => string.IsNullOrWhiteSpace(code) ? null : new string([.. code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);
}
