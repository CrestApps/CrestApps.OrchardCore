namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Resolves the automated-voice media for the provider carrying a particular call, so a tenant with more than
/// one telephony provider does not speak to a caller through the wrong one.
/// </summary>
public interface IVoiceAgentMediaProviderResolver
{
    /// <summary>
    /// Returns the media provider for the named telephony provider, or <see langword="null"/> when that provider
    /// cannot carry an automated voice conversation.
    /// </summary>
    /// <param name="technicalName">The telephony provider's technical name.</param>
    IVoiceAgentMediaProvider Get(string technicalName);

    /// <summary>
    /// Returns the only registered media provider when there is exactly one, for a call record that predates
    /// the provider name being carried. With more than one registered there is no safe guess, so this returns
    /// <see langword="null"/> rather than picking.
    /// </summary>
    IVoiceAgentMediaProvider GetDefault();
}
