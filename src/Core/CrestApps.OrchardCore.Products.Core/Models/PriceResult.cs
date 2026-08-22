namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// An immutable result of resolving the effective price of a product for a specific selling context. It
/// always pairs an amount with the currency that amount is expressed in, so a price is never passed
/// around without its currency. It is the seam a checkout or ordering flow depends on instead of reading a
/// product part's raw price, so a future pricing engine (price schedules, quantity breaks, or
/// customer-specific pricing) can produce the same result without changing the consumers.
/// </summary>
public sealed class PriceResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PriceResult"/> class.
    /// </summary>
    /// <param name="unitPrice">The effective unit price, in major currency units.</param>
    /// <param name="currency">The ISO-4217 currency code the price is expressed in. Must not be null or whitespace.</param>
    /// <param name="quantity">The quantity the price was resolved for. Values below one are treated as one.</param>
    public PriceResult(
        decimal unitPrice,
        string currency,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("A price must always carry the currency it is expressed in.", nameof(currency));
        }

        UnitPrice = unitPrice;
        Currency = currency;
        Quantity = quantity < 1 ? 1 : quantity;
    }

    /// <summary>
    /// Gets the effective unit price, in major currency units.
    /// </summary>
    public decimal UnitPrice { get; }

    /// <summary>
    /// Gets the ISO-4217 currency code the <see cref="UnitPrice"/> and <see cref="Subtotal"/> are
    /// expressed in.
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Gets the quantity the price was resolved for.
    /// </summary>
    public int Quantity { get; }

    /// <summary>
    /// Gets the subtotal for the resolved quantity, before tax.
    /// </summary>
    public decimal Subtotal => UnitPrice * Quantity;
}
