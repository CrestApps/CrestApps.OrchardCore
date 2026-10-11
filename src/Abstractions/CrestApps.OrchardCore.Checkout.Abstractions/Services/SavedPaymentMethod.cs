namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// A payment method a provider kept so it can be charged again later without the payer present. The references
/// are the provider's own and only that provider interprets them; the rest describes the method to a person.
/// </summary>
public sealed class SavedPaymentMethod
{
    /// <summary>
    /// Gets or sets the key of the provider that holds the payment method.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the provider's reference for the customer the payment method is attached to.
    /// </summary>
    public string CustomerReference { get; set; }

    /// <summary>
    /// Gets or sets the provider's reference for the payment method.
    /// </summary>
    public string PaymentMethodReference { get; set; }

    /// <summary>
    /// Gets or sets the card brand or method type, for example <c>visa</c>.
    /// </summary>
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the last four digits of the card or account.
    /// </summary>
    public string Last4 { get; set; }

    /// <summary>
    /// Gets or sets the expiration month, when the method expires.
    /// </summary>
    public int? ExpirationMonth { get; set; }

    /// <summary>
    /// Gets or sets the four-digit expiration year, when the method expires.
    /// </summary>
    public int? ExpirationYear { get; set; }

    /// <summary>
    /// Gets a short description for a person, for example <c>Visa •••• 4242</c>.
    /// </summary>
    public string Describe()
    {
        var brand = string.IsNullOrEmpty(Brand)
            ? "Card"
            : char.ToUpperInvariant(Brand[0]) + Brand[1..];

        return string.IsNullOrEmpty(Last4)
            ? brand
            : $"{brand} •••• {Last4}";
    }

    /// <summary>
    /// Gets whether the method has expired as of the given time.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    public bool IsExpired(DateTime utcNow)
    {
        if (ExpirationMonth is not int month || ExpirationYear is not int year || month is < 1 or > 12)
        {
            return false;
        }

        // A card is good through the last day of its expiration month.
        return utcNow >= new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
    }
}
