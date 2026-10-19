namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// What one checkout holds of, or took from, one balance.
/// </summary>
public sealed class CheckoutTenderHold
{
    public long Id { get; set; }

    public string ItemId { get; set; }

    public string SessionId { get; set; }

    public string ProviderKey { get; set; }

    public string Reference { get; set; }

    public string Description { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets how much is held, or was taken.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets how much of what was taken was put back.
    /// </summary>
    public decimal ReturnedAmount { get; set; }

    public CheckoutTenderHoldStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? RedeemedUtc { get; set; }
}

public enum CheckoutTenderHoldStatus
{
    Held,
    Redeemed,
    Released,
}
