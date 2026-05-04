using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Represents the omnichannel message index.
/// </summary>
public sealed class OmnichannelMessageIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the customer address.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets the service address.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is inbound.
    /// </summary>
    public bool IsInbound { get; set; }
}
