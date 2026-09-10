namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What happens to a caller the queue cannot keep: one who has waited as long as the queue is willing to make
/// anybody wait, or one who arrives when the queue is already full. Only actions the platform actually carries
/// out are listed; an option that saved and did nothing would leave a caller on hold indefinitely, which is the
/// failure this setting exists to prevent.
/// </summary>
public enum QueueMaxWaitAction
{
    /// <summary>
    /// Keep waiting. The limit is advisory and nothing is done when it is reached.
    /// </summary>
    None,

    /// <summary>
    /// Send the caller to voicemail. The queue's entry point greeting is played and the recording is delivered
    /// through the usual voicemail path.
    /// </summary>
    Voicemail,

    /// <summary>
    /// Move the caller to the first queue in the overflow chain they have not already been through, regardless
    /// of the hop's own wait threshold.
    /// </summary>
    Overflow,
}
