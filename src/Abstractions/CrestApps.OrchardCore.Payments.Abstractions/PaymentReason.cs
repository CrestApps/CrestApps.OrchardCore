namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Specifies why a payment was created or processed.
/// </summary>
public enum PaymentReason
{
    /// <summary>
    /// Indicates that the payment was created manually.
    /// </summary>
    Manual,

    /// <summary>
    /// Indicates that the payment was created as part of subscription creation.
    /// </summary>
    SubscriptionCreate,

    /// <summary>
    /// Indicates that the payment was created for a recurring subscription billing cycle.
    /// </summary>
    SubscriptionCycle,

    /// <summary>
    /// Indicates that the payment was created because an existing subscription was updated.
    /// </summary>
    SubscriptionUpdate,

    /// <summary>
    /// Indicates that the payment reason does not match another known reason.
    /// </summary>
    Other,
}
