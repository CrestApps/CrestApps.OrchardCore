namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Represents the outcome of the Telnyx "Connect" auto-provisioning flow: the resolved resource ids the
/// app writes into its settings, plus discovered numbers used to suggest a caller id.
/// </summary>
public sealed class TelnyxProvisioningResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the critical resources (Call Control application and
    /// Credential connection) were provisioned.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the resolved Call Control application id (the call <c>connection_id</c>).
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the resolved Credential (SIP) connection id used to mint browser credentials.
    /// </summary>
    public string SipConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the resolved outbound voice profile id, when one was created or found.
    /// </summary>
    public string OutboundVoiceProfileId { get; set; }

    /// <summary>
    /// Gets or sets the caller id (E.164) suggested from the account's numbers, when one is available.
    /// </summary>
    public string SuggestedCallerId { get; set; }

    /// <summary>
    /// Gets the E.164 numbers discovered on the account, used to populate the caller-id picker.
    /// </summary>
    public IList<string> AvailableNumbers { get; } = [];

    /// <summary>
    /// Gets or sets a human-readable message describing warnings from best-effort steps (outbound profile,
    /// number assignment) even when the critical resources succeeded.
    /// </summary>
    public string Warning { get; set; }

    /// <summary>
    /// Gets or sets the error message when <see cref="Succeeded"/> is <see langword="false"/>.
    /// </summary>
    public string Error { get; set; }
}
