namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the communication-session status of an interaction.
/// </summary>
public enum InteractionStatus
{
    /// <summary>
    /// The interaction has been created but no provider session is active yet.
    /// </summary>
    Created,

    /// <summary>
    /// The interaction is ringing or waiting for the remote party to answer.
    /// </summary>
    Ringing,

    /// <summary>
    /// The interaction is connected.
    /// </summary>
    Connected,

    /// <summary>
    /// The interaction is connected but temporarily on hold.
    /// </summary>
    Held,

    /// <summary>
    /// The interaction is being transferred to another destination.
    /// </summary>
    Transferring,

    /// <summary>
    /// The interaction has more than two active parties.
    /// </summary>
    Conferenced,

    /// <summary>
    /// The interaction's communication session ended.
    /// </summary>
    Ended,

    /// <summary>
    /// The interaction failed due to an error or provider failure.
    /// </summary>
    Failed,
}
