namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// The lifecycle state of a <see cref="Subscription"/>.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// The agreement is billing normally.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The customer has access but has not been billed yet because the agreement started with a trial.
    /// </summary>
    Trialing = 1,

    /// <summary>
    /// A renewal payment failed and the agreement is in its dunning window. The customer keeps access until
    /// the grace period ends, because a card that expired overnight is not the same as a customer who left.
    /// </summary>
    PastDue = 2,

    /// <summary>
    /// The agreement has been ended. Access still runs to the end of the period the customer paid for.
    /// </summary>
    Canceled = 3,

    /// <summary>
    /// The agreement ran its agreed number of cycles, or its dunning window elapsed without payment.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Billing is suspended without ending the agreement, so it can be resumed later.
    /// </summary>
    Paused = 5,

    /// <summary>
    /// The agreement was created but its first payment has never settled, so nothing is owed to the customer
    /// yet. It exists so a half-finished signup is visible rather than lost.
    /// </summary>
    Incomplete = 6,
}
