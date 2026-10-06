namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Whose leg of a call noise suppression is started on, which decides the Telnyx direction that cleans each voice.
/// </summary>
/// <remarks>
/// Telnyx names the direction from its own side of the leg: <c>outbound</c> cleans the audio Telnyx receives from the
/// party the leg reaches, and <c>inbound</c> cleans the audio Telnyx plays to that party. On the agent's leg the agent's
/// own voice is therefore <c>outbound</c>; on the customer's leg it is the other way round.
/// </remarks>
public enum TelnyxNoiseSuppressionLeg
{
    /// <summary>
    /// The leg that reaches an agent's soft phone on a call with a customer, or with a conference a customer is in.
    /// </summary>
    Agent = 0,

    /// <summary>
    /// The leg that reaches the customer.
    /// </summary>
    Customer = 1,

    /// <summary>
    /// The leg that reaches an agent's soft phone on an internal call with a colleague. The colleague's own leg cleans
    /// the colleague's voice, so only the agent's own voice is cleaned here.
    /// </summary>
    InternalAgent = 2,
}
