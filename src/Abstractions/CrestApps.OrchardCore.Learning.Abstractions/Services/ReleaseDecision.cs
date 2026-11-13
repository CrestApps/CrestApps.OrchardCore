namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Whether a release rule is met.
/// </summary>
public sealed class ReleaseDecision
{
    /// <summary>
    /// A decision that the rule is met.
    /// </summary>
    public static readonly ReleaseDecision Released = new() { IsReleased = true };

    /// <summary>
    /// Gets a value indicating whether the rule is met.
    /// </summary>
    public bool IsReleased { get; init; }

    /// <summary>
    /// Gets when the rule will be met, in UTC, when that is known.
    /// </summary>
    public DateTime? AvailableFromUtc { get; init; }

    /// <summary>
    /// Gets what the learner must do first, shown to the learner.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Creates a decision that the rule is not met.
    /// </summary>
    /// <param name="reason">What the learner must do first.</param>
    /// <param name="availableFromUtc">When the rule will be met, when that is known.</param>
    /// <returns>The decision.</returns>
    public static ReleaseDecision Locked(string reason, DateTime? availableFromUtc = null)
        => new() { IsReleased = false, Reason = reason, AvailableFromUtc = availableFromUtc };
}
