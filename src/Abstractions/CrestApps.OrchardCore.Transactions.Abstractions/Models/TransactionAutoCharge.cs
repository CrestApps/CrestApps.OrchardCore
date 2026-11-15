namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A charge of the owner's saved payment method scheduled on a transaction by
/// <see cref="Services.ISavedPaymentMethodChargeService"/>, with the attempts made so far. It is removed once the charge
/// goes through, or once the retries run out and the owner is asked to pay themselves.
/// </summary>
public sealed class TransactionAutoCharge
{
    /// <summary>
    /// Gets or sets the identifier of the owner's saved payment method to charge.
    /// </summary>
    public string SavedPaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets when the next charge is made, in UTC.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets how many charges were declined so far. A charge that could not be made at all (the provider could
    /// not be reached) does not count.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets when the last charge was made, in UTC.
    /// </summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last charge was not taken.
    /// </summary>
    public string LastFailure { get; set; }
}
