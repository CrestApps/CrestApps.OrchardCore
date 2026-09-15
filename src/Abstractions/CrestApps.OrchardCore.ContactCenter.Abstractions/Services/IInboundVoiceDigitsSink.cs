namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Receives the key a caller pressed on an entry-point menu.
/// </summary>
/// <remarks>
/// Separate from the inbound call sink because it arrives on a different provider event and means something
/// different: the call is already tracked, and this says where in the menu the caller has got to.
/// </remarks>
public interface IInboundVoiceDigitsSink
{
    /// <summary>
    /// Applies a key press to whatever menu the caller is in.
    /// </summary>
    /// <param name="digitsEvent">What the caller pressed, and on which call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the press belonged to a menu and was applied.</returns>
    Task<bool> HandleDigitsAsync(InboundVoiceDigitsEvent digitsEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// A key press reported by a telephony provider.
/// </summary>
public sealed class InboundVoiceDigitsEvent
{
    /// <summary>
    /// Gets or sets the technical name of the provider that reported it.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the caller's leg.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets what the caller pressed. Empty when they pressed nothing before the menu timed out, which is
    /// a missed choice rather than a selection.
    /// </summary>
    public string Digits { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for this delivery, so a redelivery is recognised as one the caller
    /// has already been advanced by.
    /// </summary>
    public string DeliveryId { get; set; }
}
