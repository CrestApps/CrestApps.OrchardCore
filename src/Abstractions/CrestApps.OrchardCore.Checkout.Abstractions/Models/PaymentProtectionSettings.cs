namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// Settings › Commerce › Checkout › Payment protection: what the store does when card payments are declined across the
/// store far more often than usual, the sign of card testers rotating networks.
/// </summary>
public sealed class PaymentProtectionSettings
{
    /// <summary>
    /// Gets or sets what happens while the store-wide decline limit is reached. Each choice records a payment issue.
    /// </summary>
    public DeclineSpikeAction OnDeclineSpike { get; set; } = DeclineSpikeAction.Notify;
}

/// <summary>
/// What the store does during a store-wide run of declined card payments.
/// </summary>
public enum DeclineSpikeAction
{
    /// <summary>
    /// Record a payment issue for the staff and keep taking payments.
    /// </summary>
    Notify,

    /// <summary>
    /// Guests must sign in to pay by card; signed-in customers pay as usual.
    /// </summary>
    RequireSignIn,

    /// <summary>
    /// Guests can't pay by card for now and are asked to use another way to pay; signed-in customers pay as usual.
    /// </summary>
    PauseGuestCards,
}
