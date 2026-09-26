using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel channel endpoint.
/// </summary>
public sealed class OmnichannelChannelEndpoint : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel, ICloneable<OmnichannelChannelEndpoint>
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the messaging/telephony provider that owns this number (for example
    /// "Twilio", "Telnyx", or "AzureCommunicationServices"). When empty, the tenant-default provider is used.
    /// The SMS portal's dispatcher reads this to route an outbound send through the provider that owns the
    /// sending number.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified utc.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current channel endpoint.
    /// </summary>
    public OmnichannelChannelEndpoint Clone()
    {
        return new OmnichannelChannelEndpoint
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Channel = Channel,
            Value = Value,
            Description = Description,
            ProviderName = ProviderName,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
