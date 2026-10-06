namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A request to complete the activity of a dialer attempt that ended before an agent was connected.
/// </summary>
public sealed class DialerAttemptFinalizationRequest
{
    /// <summary>
    /// Gets or sets the activity the attempt was for.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the interaction of the attempt, when a call was created for it.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets how the attempt ended, one of <see cref="DialerAttemptOutcomes"/>.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets the terminal reason to record instead of the one for <see cref="Outcome"/>.
    /// </summary>
    public string TerminalReasonCode { get; set; }

    /// <summary>
    /// Gets or sets the number the attempt dialed, when it is known.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets what the provider or the platform said about the ending, for the activity notes.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets what reported the ending, for the logs.
    /// </summary>
    public string Trigger { get; set; }
}
