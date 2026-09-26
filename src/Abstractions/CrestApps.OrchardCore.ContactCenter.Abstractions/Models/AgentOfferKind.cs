using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Classifies a work offer so the agent experience can present the correct call-to-action and screen-pop
/// behavior. The distinction maps to the contact-center dialing modes: an inbound call and a preview dial
/// both wait for an explicit agent action, while a power, progressive, or predictive dial is answered by the
/// system and only pops the record.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentOfferKind
{
    /// <summary>
    /// An inbound call ringing the agent. The agent explicitly accepts to answer or declines to release it.
    /// </summary>
    InboundCall,

    /// <summary>
    /// A preview dial. The agent reviews the record, then dials (accepts) or skips (declines) before any call
    /// is placed.
    /// </summary>
    PreviewDial,

    /// <summary>
    /// A system-paced dial (power, progressive, or predictive). The call is placed and connected by the dialer,
    /// so the agent takes no accept action; the experience only pops the record.
    /// </summary>
    AutoDial,
}
