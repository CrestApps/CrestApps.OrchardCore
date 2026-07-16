namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the provider-neutral outcome of automated answer detection for an outbound voice call.
/// AMD (Answering Machine Detection) classifies how or whether the remote party answered so the
/// dialer, compliance, and analytics layers can take the correct follow-up action regardless of
/// the telephony provider that reported it.
/// </summary>
public enum AnswerClassification
{
    /// <summary>
    /// A live person answered the call.
    /// </summary>
    Human,

    /// <summary>
    /// An answering machine or voicemail greeting was detected instead of a live person.
    /// </summary>
    Machine,

    /// <summary>
    /// A fax machine tone was detected.
    /// </summary>
    Fax,

    /// <summary>
    /// Detection completed but the outcome could not be determined with sufficient confidence.
    /// </summary>
    Unknown,
}
