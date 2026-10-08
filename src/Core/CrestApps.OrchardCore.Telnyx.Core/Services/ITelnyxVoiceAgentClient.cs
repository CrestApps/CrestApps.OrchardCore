namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Places the outbound call an automated voice conversation runs on.
/// <para>
/// Everything that happens once the call is up — speaking, transcribing, hanging up — is provider-neutral and
/// goes through the voice agent media abstraction instead, so this contract covers only the part that is not:
/// dialling out with the Telnyx connection, caller id, and outbound voice profile.
/// </para>
/// </summary>
public interface ITelnyxVoiceAgentClient
{
    /// <summary>
    /// Originates an outbound call for the automated voice agent and returns the new leg's call-control id.
    /// </summary>
    /// <param name="to">The destination address (the customer's phone number).</param>
    /// <param name="from">The caller id to present, or <see langword="null"/> to use the configured default.</param>
    /// <param name="clientState">The AI-voice client state Telnyx echoes back on every event for the leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The originated call's call-control id, or <see langword="null"/> when origination failed.</returns>
    Task<string> OriginateAsync(string to, string from, TelnyxOutboundBridgeState clientState, CancellationToken cancellationToken = default);
}
