namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// How an automated call asks the provider whether a person or a machine answered it.
/// </summary>
public enum TelnyxAnsweringMachineDetection
{
    /// <summary>
    /// The provider's premium detection, which classifies the answer with speech recognition and reports when the
    /// greeting's tone sounds. The default, because every other way of telling a voicemail from a person on this
    /// platform has to wait for the greeting to be transcribed, and a short greeting is over before that happens.
    /// </summary>
    Premium = 0,

    /// <summary>
    /// The provider's standard detection, listening for the greeting to end on silence or a tone.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// No provider detection. A voicemail is still recognised from its greeting, or from a line nobody speaks on.
    /// </summary>
    Disabled = 2,
}
