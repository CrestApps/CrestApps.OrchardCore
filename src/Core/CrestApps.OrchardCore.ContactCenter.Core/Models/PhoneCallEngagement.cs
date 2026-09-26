using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A supervisor listening to, coaching or on an agent's own phone call: a number the agent dialed from the keypad, or an
/// extension call. Such a call is no Contact Center interaction and has no call session to record the engagement on,
/// so it is kept on its own, for as long as the call lasts.
/// </summary>
public sealed class PhoneCallEngagement
{
    /// <summary>
    /// Gets or sets the call, as the soft phone's call history knows it: the leg of the agent who placed it.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the voice provider of the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the user being monitored.
    /// </summary>
    public string MonitoredUserId { get; set; }

    /// <summary>
    /// Gets or sets the agent profile of the user being monitored.
    /// </summary>
    public string MonitoredAgentId { get; set; }

    /// <summary>
    /// Gets or sets the supervisor.
    /// </summary>
    public string SupervisorUserId { get; set; }

    /// <summary>
    /// Gets or sets how the supervisor is on the call.
    /// </summary>
    public MonitorMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the one-off token the supervisor's phone answers its leg by.
    /// </summary>
    public string MonitorToken { get; set; }

    /// <summary>
    /// Gets or sets the supervisor's own leg, once it was rung.
    /// </summary>
    public string SupervisorLegId { get; set; }

    /// <summary>
    /// Gets or sets the call as the provider's monitoring requests name it.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the leg of the agent being monitored.
    /// </summary>
    public string AgentLegId { get; set; }

    /// <summary>
    /// Gets or sets the leg of the other party.
    /// </summary>
    public string OtherPartyLegId { get; set; }

    /// <summary>
    /// Gets or sets the conference the supervisor joins.
    /// </summary>
    public string ConferenceName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the supervisor can take the call over.
    /// </summary>
    public bool CanTakeOver { get; set; }

    /// <summary>
    /// Gets or sets when the engagement was started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the supervisor's phone answered its leg.
    /// </summary>
    public DateTime? ConnectedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the supervisor took the call over: it is theirs now.
    /// </summary>
    public bool TookOver { get; set; }
}
