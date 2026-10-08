namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// An outcome the platform can reach on its own, and records with the disposition marked for it.
/// </summary>
/// <remarks>
/// A disposition's name is free text, so the platform cannot find "the not-in-service one" or "the no-answer one" by
/// reading it. The outcome is how it finds them: when a call ends in a way nobody chose -- the network reports the
/// number dead, nobody answers an automated call, a voicemail picks up -- the platform applies the disposition with the
/// matching outcome, preferring one the subject's flow is wired to so that subject's actions run. Only outcomes the
/// platform actually produces belong here; one only a person can choose is an ordinary disposition.
/// </remarks>
public enum DispositionOutcome
{
    /// <summary>
    /// An ordinary disposition the platform never applies on its own.
    /// </summary>
    None = 0,

    /// <summary>
    /// The number is not in service. The dialer and automated calls apply it when the network reports the number
    /// unallocated or invalid, and recording it -- by the platform or a person -- adds the number to the list of numbers
    /// that are never loaded or dialed again.
    /// </summary>
    NotInService = 1,

    /// <summary>
    /// Nobody answered. Automated calls apply it to a call that rang out or was never spoken on.
    /// </summary>
    NoAnswer = 2,

    /// <summary>
    /// The line was busy. Automated calls apply it when the network reports the called party busy.
    /// </summary>
    Busy = 3,

    /// <summary>
    /// A voicemail or answering machine picked up. Automated calls apply it to a call that reached voicemail, and the
    /// dialer applies it to a call its answering-machine detection screened out.
    /// </summary>
    AnsweringMachine = 4,

    /// <summary>
    /// The called party or the network declined the call. The dialer applies it to an attempt the network rejected.
    /// </summary>
    Rejected = 5,

    /// <summary>
    /// The call could not be completed: the provider refused to place it, or the network failed it for another reason
    /// such as congestion. The dialer applies it to an attempt that failed that way.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// The customer answered but the call ended before an agent was connected to it. The dialer applies it to an
    /// attempt the customer hung up on while it was being connected.
    /// </summary>
    Disconnected = 7,
}
