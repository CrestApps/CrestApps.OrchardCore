using YesSql.Indexes;

namespace CrestApps.OrchardCore.Asterisk.Indexes;

/// <summary>
/// YesSql index used to query durable Asterisk channel ownership bindings in the current tenant store.
/// </summary>
public sealed class AsteriskChannelTenantBindingIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the YesSql document identifier for the binding document.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk channel identifier used as the natural key for the binding.
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the configured provider name that owns the channel.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the contact-center interaction identifier associated with the channel.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the peer channel this channel is bridged to, enabling an indexed reverse
    /// lookup so either leg's terminal event can release the whole call.
    /// </summary>
    public string PeerChannelId { get; set; }
}
