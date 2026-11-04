using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of a single outbound dialing attempt: whether the call was started and, when the
/// compliance gate refused it, why.
/// </summary>
public sealed class DialerAttemptResult
{
    /// <summary>
    /// Gets a result for an attempt whose call was started.
    /// </summary>
    public static DialerAttemptResult Dialed { get; } = new() { Started = true };

    /// <summary>
    /// Gets a result for an attempt that was not started for a reason other than the compliance gate.
    /// </summary>
    public static DialerAttemptResult NotStarted { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the outbound call was started.
    /// </summary>
    public bool Started { get; private init; }

    /// <summary>
    /// Gets the compliance decision that refused the attempt, or <see langword="null"/> when the gate did not refuse it.
    /// </summary>
    public DialerEligibilityResult Suppression { get; private init; }

    /// <summary>
    /// Creates a result for an attempt the compliance gate refused.
    /// </summary>
    /// <param name="suppression">The compliance decision that refused the attempt.</param>
    /// <returns>The result.</returns>
    public static DialerAttemptResult Suppressed(DialerEligibilityResult suppression)
    {
        ArgumentNullException.ThrowIfNull(suppression);

        return new DialerAttemptResult { Suppression = suppression };
    }
}
