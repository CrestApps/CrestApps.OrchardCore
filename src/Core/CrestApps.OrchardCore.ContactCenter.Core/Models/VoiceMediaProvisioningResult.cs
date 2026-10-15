namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The outcome of making sure a telephony provider holds a copy of a clip.
/// </summary>
public sealed class VoiceMediaProvisioningResult
{
    /// <summary>
    /// Gets the technical name the provider's copy is recorded under, or <see langword="null"/> when there is none.
    /// </summary>
    public string ProviderName { get; init; }

    /// <summary>
    /// Gets the reference the provider plays the clip by, or <see langword="null"/> when nothing can play.
    /// </summary>
    public string Reference { get; init; }

    /// <summary>
    /// Gets the reference of the copy the provider reported missing, which the copy in <see cref="Reference"/> was
    /// made to replace; <see langword="null"/> when no copy was recorded for the provider before.
    /// </summary>
    public string ReplacedReference { get; init; }

    /// <summary>
    /// Gets where the clip stands with the provider.
    /// </summary>
    public VoiceMediaProvisioningStatus Status { get; init; }

    /// <summary>
    /// Gets a value indicating whether the clip's storage state was changed, which is the case when a copy was
    /// uploaded to the provider. The caller that owns the record the clip belongs to persists it then.
    /// </summary>
    public bool Changed { get; init; }
}
