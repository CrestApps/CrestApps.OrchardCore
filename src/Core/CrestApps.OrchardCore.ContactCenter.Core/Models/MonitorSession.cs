using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a supervisor's live engagement with a call. Without it a supervisor's presence exists only
/// as a pair of past events, so nothing can answer who is listening to a call right now, stop an
/// engagement the supervisor's browser lost, or prevent the same supervisor engaging twice.
/// </summary>
public sealed class MonitorSession
{
    /// <summary>
    /// Gets or sets the platform identifier of the engagement.
    /// </summary>
    public string MonitorSessionId { get; set; }

    /// <summary>
    /// Gets or sets the supervisor's agent-profile identifier, when the supervisor has an agent profile.
    /// This is the identifier that shares an identity space with <see cref="TargetAgentId"/>.
    /// </summary>
    public string SupervisorAgentId { get; set; }

    /// <summary>
    /// Gets or sets the supervisor's user identifier. A supervisor is always a user but is not always an
    /// agent, so this is the identifier the engagement is matched on when it is started and stopped.
    /// </summary>
    public string SupervisorUserId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent being monitored.
    /// </summary>
    public string TargetAgentId { get; set; }

    /// <summary>
    /// Gets or sets the way the supervisor is engaged with the call.
    /// </summary>
    public MonitorMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the provider identifier of the supervisor's leg, when the provider reports one.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the engagement started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the engagement ended, or <see langword="null"/> while it is live.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether the engagement is still live.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => EndedUtc is null;
}
