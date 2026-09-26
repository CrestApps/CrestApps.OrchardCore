namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Tells a supervisor's own clients -- their soft phone and their live dashboard -- about an engagement of theirs: that
/// their phone is about to be rung for it, that they are on the call, that its mode changed, that they took the call
/// over, or that it ended.
/// </summary>
public sealed class SupervisorEngagementNotification
{
    /// <summary>
    /// Their phone is about to be rung for the engagement: it answers the leg carrying <see cref="MonitorToken"/> by itself.
    /// </summary>
    public const string Requested = "Requested";

    /// <summary>
    /// The supervisor's phone answered and the supervisor is on the call.
    /// </summary>
    public const string Connected = "Connected";

    /// <summary>
    /// The engagement changed mode on the same leg.
    /// </summary>
    public const string ModeChanged = "ModeChanged";

    /// <summary>
    /// The supervisor took the call over and is now the agent handling it.
    /// </summary>
    public const string TookOver = "TookOver";

    /// <summary>
    /// The engagement ended, or could not be started.
    /// </summary>
    public const string Ended = "Ended";

    /// <summary>
    /// Gets or sets what happened, one of the constants on this type.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the interaction the engagement is on.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the supervisor's user identifier, whose connections receive the notification.
    /// </summary>
    public string SupervisorUserId { get; set; }

    /// <summary>
    /// Gets or sets the agent-profile identifier of the agent whose call it is.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the name of the agent whose call it is, for the supervisor's phone to show.
    /// </summary>
    public string AgentName { get; set; }

    /// <summary>
    /// Gets or sets the engagement's mode by its stable name: Monitor, Whisper or Barge.
    /// </summary>
    public string Mode { get; set; }

    /// <summary>
    /// Gets or sets the one-off token on the leg the supervisor's phone is rung on, for <see cref="Requested"/>.
    /// </summary>
    public string MonitorToken { get; set; }

    /// <summary>
    /// Gets or sets why an engagement ended, when it did not end at the supervisor's request.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the notification was sent.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
