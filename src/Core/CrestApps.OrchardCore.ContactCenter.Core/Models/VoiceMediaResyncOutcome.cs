namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a re-sync did for one telephony provider.
/// </summary>
public enum VoiceMediaResyncOutcome
{
    /// <summary>
    /// The provider confirmed it still holds its copy; nothing was uploaded.
    /// </summary>
    Verified,

    /// <summary>
    /// The provider had no copy, or had lost it, and was given one from the master copy.
    /// </summary>
    Uploaded,

    /// <summary>
    /// The provider could not be given a copy: the master copy could not be read or the provider refused the upload.
    /// </summary>
    Failed,
}
