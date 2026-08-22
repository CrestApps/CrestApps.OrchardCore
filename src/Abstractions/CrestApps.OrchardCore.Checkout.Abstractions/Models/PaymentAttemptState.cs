namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// The lifecycle state of a durable <see cref="PaymentAttempt"/>. The attempt is the authoritative,
/// persisted record of an interaction with a payment provider. It is written <em>before</em> the provider
/// is ever called so a crash, cache loss, or node failure can never strand a real charge as an
/// unrecorded "orphan".
/// </summary>
public enum PaymentAttemptState
{
    /// <summary>
    /// The attempt has been persisted but the provider has not been called yet.
    /// </summary>
    Created,

    /// <summary>
    /// The provider has been called and the outcome is not yet known (awaiting confirmation/webhook).
    /// </summary>
    Pending,

    /// <summary>
    /// The provider confirmed the charge succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The provider reported the charge failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The attempt was canceled before it succeeded.
    /// </summary>
    Canceled,
}
