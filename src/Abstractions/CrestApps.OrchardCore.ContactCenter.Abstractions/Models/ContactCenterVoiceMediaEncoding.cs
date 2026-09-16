namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the audio encoding exchanged with a Contact Center voice media provider.
/// </summary>
public enum ContactCenterVoiceMediaEncoding
{
    /// <summary>
    /// The encoding is not known.
    /// </summary>
    Unknown,

    /// <summary>
    /// Signed linear pulse-code modulation.
    /// </summary>
    LinearPcm,

    /// <summary>
    /// G.711 mu-law pulse-code modulation.
    /// </summary>
    MuLaw,

    /// <summary>
    /// G.711 A-law pulse-code modulation.
    /// </summary>
    ALaw,
}
