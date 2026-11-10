namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// Who asks for a payment handler, and for which checkout.
/// </summary>
public sealed class AgentPaymentHandlerContext
{
    /// <summary>
    /// Gets or sets the protocol, one of <see cref="AgenticCommerceConstants.Protocols"/>.
    /// </summary>
    public string Protocol { get; set; }

    /// <summary>
    /// Gets or sets the protocol version.
    /// </summary>
    public string ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets the agent platform's id.
    /// </summary>
    public string PlatformId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the platform is in test mode, where only test handlers are offered.
    /// </summary>
    public bool TestMode { get; set; }

    /// <summary>
    /// Gets or sets the checkout's currency, or <see langword="null"/> when describing the store.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what the checkout costs, or <see langword="null"/> when describing the store.
    /// </summary>
    public decimal? Total { get; set; }
}
