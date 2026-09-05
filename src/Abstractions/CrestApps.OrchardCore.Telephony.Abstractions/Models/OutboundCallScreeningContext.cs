namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Carries the data an <see cref="IOutboundCallScreener"/> needs to decide whether an outbound
/// origination may proceed. It is built at the shared telephony boundary so every origination path is
/// screened identically, regardless of which surface initiated it.
/// </summary>
public sealed class OutboundCallScreeningContext
{
    /// <summary>
    /// Gets or sets the dial request being screened.
    /// </summary>
    public DialRequest Request { get; set; }

    /// <summary>
    /// Gets or sets how the origination reached the shared telephony boundary.
    /// </summary>
    public OutboundCallOrigin Origin { get; set; }
}
