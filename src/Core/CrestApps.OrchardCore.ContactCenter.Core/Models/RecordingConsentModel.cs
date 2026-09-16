namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the consent model a tenant must satisfy before a voice interaction may be recorded.
/// </summary>
public enum RecordingConsentModel
{
    /// <summary>
    /// Every party on the call must consent before recording is permitted (two-party or all-party jurisdictions).
    /// </summary>
    AllParties,

    /// <summary>
    /// A single party's consent (the recording organization) is sufficient to permit recording.
    /// </summary>
    SingleParty,
}
