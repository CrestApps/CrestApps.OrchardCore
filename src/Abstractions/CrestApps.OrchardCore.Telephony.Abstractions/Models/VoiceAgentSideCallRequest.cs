namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// A call placed beside an automated call's caller, to reach the person a warm or screened transfer goes to.
/// </summary>
public sealed class VoiceAgentSideCallRequest
{
    /// <summary>
    /// Gets or sets the provider's identifier for the caller's leg the side call is for.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the activity of the automated call, so the side call's events reach its conversation.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the number to ring, in E.164 form.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the number shown to the person rung, or <see langword="null"/> for the provider's default.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets how long the number rings before the side call is given up.
    /// </summary>
    public int RingTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether the provider should tell a person from an answering machine.
    /// </summary>
    public bool DetectAnsweringMachine { get; set; } = true;
}
