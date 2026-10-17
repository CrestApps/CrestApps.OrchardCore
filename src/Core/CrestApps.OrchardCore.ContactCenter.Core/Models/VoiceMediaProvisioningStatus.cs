namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Where a clip stands with the telephony provider that was asked to play it.
/// </summary>
public enum VoiceMediaProvisioningStatus
{
    /// <summary>
    /// The provider holds a copy made from the master copy, and the reference plays it.
    /// </summary>
    Provisioned,

    /// <summary>
    /// The clip has no master copy, so the copy the provider was given when the clip was uploaded is all there is.
    /// The reference plays it for as long as the provider keeps it; it cannot be checked or made again.
    /// </summary>
    ProviderCopyOnly,

    /// <summary>
    /// Nothing can play: the clip has no master copy and this provider was never given a copy. This is what a clip
    /// uploaded before master copies were kept comes to after the provider is changed; the clip has to be uploaded
    /// again.
    /// </summary>
    NoAudio,

    /// <summary>
    /// The master copy exists but could not be delivered to the provider: no provisioner is registered for it, the
    /// master copy could not be read, or the provider refused the upload. The next use tries again.
    /// </summary>
    Unavailable,
}
