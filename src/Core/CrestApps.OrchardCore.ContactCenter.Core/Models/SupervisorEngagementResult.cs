namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of a supervisor live-monitoring engagement.
/// </summary>
public sealed class SupervisorEngagementResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the engagement was accepted.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets an explanation of the outcome.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful <see cref="SupervisorEngagementResult"/>.</returns>
    public static SupervisorEngagementResult Success()
    {
        return new SupervisorEngagementResult { Succeeded = true };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The failure reason.</param>
    /// <returns>A failed <see cref="SupervisorEngagementResult"/>.</returns>
    public static SupervisorEngagementResult Failure(string reason)
    {
        return new SupervisorEngagementResult { Succeeded = false, Reason = reason };
    }
}
