namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The result of evaluating an address against the dial destination policy: the verdict and the reason to show
/// the caller when the address is refused.
/// </summary>
public sealed class DialDestinationDecision
{
    private DialDestinationDecision(DialDestinationOutcome outcome, string reason)
    {
        Outcome = outcome;
        Reason = reason;
    }

    /// <summary>
    /// Gets the verdict for the address.
    /// </summary>
    public DialDestinationOutcome Outcome { get; }

    /// <summary>
    /// Gets the reason the address was refused, or <see langword="null"/> when it was allowed.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets a value indicating whether the address may be dialed.
    /// </summary>
    public bool IsAllowed => Outcome == DialDestinationOutcome.Allowed;

    /// <summary>
    /// Creates an allowing decision.
    /// </summary>
    /// <returns>The decision.</returns>
    public static DialDestinationDecision Allow()
        => new(DialDestinationOutcome.Allowed, null);

    /// <summary>
    /// Creates a refusing decision.
    /// </summary>
    /// <param name="outcome">The verdict, which must not be <see cref="DialDestinationOutcome.Allowed"/>.</param>
    /// <param name="reason">The reason to show the caller.</param>
    /// <returns>The decision.</returns>
    public static DialDestinationDecision Refuse(DialDestinationOutcome outcome, string reason)
        => new(outcome, reason);
}
