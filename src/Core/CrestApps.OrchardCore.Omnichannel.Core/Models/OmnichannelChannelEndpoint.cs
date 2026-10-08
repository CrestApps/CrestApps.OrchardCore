using System.Text.Json;
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
    /// Gets or sets the single channel of an address saved before addresses carried capabilities. It is read to bring
    /// such a record forward, and nothing new is written to it: <see cref="AddressType"/> and <see cref="Capabilities"/>
    /// say what the address is now.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the kind of address this is, such as a phone number (see <see cref="OmnichannelAddressTypes"/>). It
    /// decides how the value is stored and which capabilities the address can have, and is fixed once created.
    /// </summary>
    public string AddressType { get; set; }

    /// <summary>
    /// Gets or sets what the business does with this address, named by channel: <c>Phone</c> for calls and <c>SMS</c>
    /// for texts on a phone number. One record holds every capability of an address, so a number used for calls and
    /// texts is one address rather than two.
    /// </summary>
    public IList<string> Capabilities { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifiers of the records that were merged into this one when the same address was listed once
    /// per channel. Activities and other history that still name one of them find this address.
    /// </summary>
    public IList<string> MergedItemIds { get; set; } = [];

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
    /// Gets the kind of address this is, reading the channel of a record saved before addresses had a type.
    /// </summary>
    public string GetAddressType()
        => !string.IsNullOrEmpty(AddressType)
            ? AddressType
            : OmnichannelAddressTypes.FromChannel(Channel);

    /// <summary>
    /// Gets the capabilities of the address, reading the channel of a record saved before addresses had capabilities.
    /// </summary>
    public IReadOnlyList<string> GetCapabilities()
        => Capabilities is { Count: > 0 }
            ? [.. Capabilities]
            : string.IsNullOrEmpty(Channel) ? [] : [Channel];

    /// <summary>
    /// Determines whether the business uses this address on a channel.
    /// </summary>
    /// <param name="capability">The capability, named by channel (for example <c>Phone</c> or <c>SMS</c>).</param>
    public bool HasCapability(string capability)
        => !string.IsNullOrEmpty(capability) &&
            GetCapabilities().Contains(capability, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether this address is, or absorbed, the record with an identifier.
    /// </summary>
    /// <param name="itemId">The record identifier.</param>
    public bool IsKnownAs(string itemId)
        => !string.IsNullOrEmpty(itemId) &&
            (string.Equals(ItemId, itemId, StringComparison.Ordinal) ||
                (MergedItemIds?.Contains(itemId, StringComparer.Ordinal) ?? false));

    /// <summary>
    /// Gets every identifier the address is known by: its own, and those of the records merged into it.
    /// </summary>
    public IReadOnlyCollection<string> GetKnownIds()
        => [ItemId, .. (MergedItemIds ?? []).Where(id => !string.IsNullOrEmpty(id))];

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
            AddressType = AddressType,
            Capabilities = Capabilities is null ? [] : [.. Capabilities],
            MergedItemIds = MergedItemIds is null ? [] : [.. MergedItemIds],
            Value = Value,
            Description = Description,
            ProviderName = ProviderName,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
            // Everything a driver stores with Put lives here; a clone without it drops those settings on every save.
            Properties = Properties is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Properties)),
        };
    }
}
