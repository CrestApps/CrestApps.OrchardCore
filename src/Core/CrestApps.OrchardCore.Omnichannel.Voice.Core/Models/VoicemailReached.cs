namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// A durable marker stored on the automated voice activity's property bag once the call is known to have been
/// answered by voicemail or an answering machine.
/// </summary>
/// <remarks>
/// It outlives the provider event that detected it: the greeting is heard on one transcription, the message is left
/// on a later turn or once the line goes quiet for the tone, and the call is concluded after the hangup. Each of those
/// steps reads it to know that nobody on this call is the customer.
/// </remarks>
public sealed class VoicemailReached
{
    /// <summary>
    /// Gets or sets whether the assistant has left its message on the recording.
    /// </summary>
    public bool MessageLeft { get; set; }
}
