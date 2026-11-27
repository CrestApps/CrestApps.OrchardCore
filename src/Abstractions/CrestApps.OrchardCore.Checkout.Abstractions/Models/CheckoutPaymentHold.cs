namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// The checkout whose payment for a record (an order, a transaction) began last, kept on that record. While that
/// payment may still take the money, another checkout of the same record (a second tab of its pay link) isn't let
/// through to pay it too. <see cref="Services.ICheckoutPaymentHoldService"/> answers whether it still holds.
/// </summary>
public sealed class CheckoutPaymentHold
{
    /// <summary>
    /// How long a hold whose checkout has no payment attempt yet keeps others out: as long as the checkout's own lock
    /// lets its payment run before the attempts it made are committed.
    /// </summary>
    public static readonly TimeSpan BeginWindow = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long a payment that was begun but not finished, such as a card waiting on 3-D Secure or an intent the payer
    /// never confirmed, keeps other checkouts of the record out. After it, the next checkout cancels that payment at the
    /// provider and goes ahead, so an abandoned payment can't block the record for good. It is counted from the first
    /// time the holding checkout began, so beginning again from the same checkout doesn't extend it.
    /// </summary>
    public static readonly TimeSpan PendingHoldLimit = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the checkout session whose payment began.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets when its payment began.
    /// </summary>
    public DateTime HeldUtc { get; set; }
}
