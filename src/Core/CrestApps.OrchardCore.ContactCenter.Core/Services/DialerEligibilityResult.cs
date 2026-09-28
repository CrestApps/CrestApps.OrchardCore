using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Represents the outcome of an outbound dialer compliance evaluation.
/// </summary>
public sealed class DialerEligibilityResult
{
    /// <summary>
    /// Gets a value indicating whether the activity may be dialed.
    /// </summary>
    public bool IsEligible { get; private init; }

    /// <summary>
    /// Gets the reason the attempt was suppressed, or <see cref="DialerSuppressionReason.None"/> when eligible.
    /// </summary>
    public DialerSuppressionReason Reason { get; private init; }

    /// <summary>
    /// Gets a human-readable explanation of the suppression decision.
    /// </summary>
    public string Description { get; private init; }

    /// <summary>
    /// Creates an eligible result.
    /// </summary>
    /// <returns>An eligible <see cref="DialerEligibilityResult"/>.</returns>
    public static DialerEligibilityResult Eligible()
    {
        return new DialerEligibilityResult
        {
            IsEligible = true,
            Reason = DialerSuppressionReason.None,
        };
    }

    /// <summary>
    /// Creates a suppressed result.
    /// </summary>
    /// <param name="reason">The reason the attempt was suppressed.</param>
    /// <param name="description">A human-readable explanation of the suppression decision.</param>
    /// <returns>A suppressed <see cref="DialerEligibilityResult"/>.</returns>
    public static DialerEligibilityResult Suppressed(DialerSuppressionReason reason, string description)
    {
        return new DialerEligibilityResult
        {
            IsEligible = false,
            Reason = reason,
            Description = description,
        };
    }
}
