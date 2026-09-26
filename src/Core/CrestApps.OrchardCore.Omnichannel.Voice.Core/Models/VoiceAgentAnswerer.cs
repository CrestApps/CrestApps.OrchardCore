namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// Who the provider says answered an automated call.
/// </summary>
public enum VoiceAgentAnswerer
{
    /// <summary>
    /// The provider could not tell, or did not say.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A person picked up.
    /// </summary>
    Person,

    /// <summary>
    /// A voicemail, answering machine, or anything else that is not somebody to talk to.
    /// </summary>
    Machine,
}
