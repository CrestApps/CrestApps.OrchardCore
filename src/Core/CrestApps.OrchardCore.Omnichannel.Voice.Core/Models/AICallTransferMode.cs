namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// How an AI call is transferred to a phone number.
/// </summary>
public enum AICallTransferMode
{
    /// <summary>
    /// The caller is connected to whoever answers, and the AI drops off at once.
    /// </summary>
    Cold,

    /// <summary>
    /// The number is rung on a call of its own while the caller holds; whoever answers hears a private briefing, and
    /// then the two are connected.
    /// </summary>
    Warm,

    /// <summary>
    /// Like <see cref="Warm"/>, but whoever answers is asked whether they can take the call; on no, or no answer, the
    /// caller goes back to the AI with the reason.
    /// </summary>
    Screened,
}
