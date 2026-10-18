using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Where a call the dialer placed for a reserved agent stands before the agent is connected to it. A power,
/// progressive or predictive dial holds its agent while the customer is reached, so the agent's screens show what the
/// dialer is doing rather than a call the agent is not on yet.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DialAttemptPhase
{
    /// <summary>
    /// The call is not a dialer attempt waiting for its agent: the agent is on it, or it is not a campaign dial.
    /// </summary>
    None,

    /// <summary>
    /// The customer's phone is ringing.
    /// </summary>
    Dialing,

    /// <summary>
    /// The customer answered and the provider is screening the answer for an answering machine. The agent is
    /// connected only if a person answered.
    /// </summary>
    Screening,

    /// <summary>
    /// A person answered and the agent's leg is being joined to the call.
    /// </summary>
    Connecting,
}
