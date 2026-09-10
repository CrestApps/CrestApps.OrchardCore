namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a request to connect (bridge) a live provider call to a selected agent. The Contact
/// Center owns the activity, queue, agent, and reservation decisions; the provider only executes the
/// connect operation when its delivery model is <see cref="VoiceProviderDeliveryModel.ServerSideAcd"/>.
/// </summary>
public sealed class ContactCenterConnectRequest
{
    /// <summary>
    /// Gets or sets the CRM activity identifier the call belongs to.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction identifier the call belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier of the live call to connect.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent profile receiving the call.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Orchard user the agent profile represents.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the provider-side agent endpoint to ring or bridge (for example an extension or SIP address), when known.
    /// </summary>
    public string AgentEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier the call is being delivered from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata for the connect request.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
