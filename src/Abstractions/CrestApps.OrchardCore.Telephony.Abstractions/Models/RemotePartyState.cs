using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Where the other party of a call the platform connects on the agent's behalf stands, while the agent's own leg is
/// already up.
/// </summary>
/// <remarks>
/// A number dialed from the soft phone (and an extension call) rings the agent's own phone first, which answers at once,
/// and only then dials the other party. The agent's call reads as connected throughout, so without this the soft phone
/// cannot tell a number that is still ringing from one that answered and stays silent.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RemotePartyState
{
    /// <summary>
    /// The other party is being rung and has not answered yet.
    /// </summary>
    Ringing = 0,

    /// <summary>
    /// The other party answered and is connected to the agent.
    /// </summary>
    Answered = 1,

    /// <summary>
    /// The other party's leg ended: busy, unanswered, refused, not in service, or hung up after the conversation.
    /// </summary>
    Ended = 2,
}
