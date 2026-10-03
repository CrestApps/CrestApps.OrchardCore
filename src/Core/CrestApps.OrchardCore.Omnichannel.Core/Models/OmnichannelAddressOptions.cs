using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Holds the address types and capabilities registered by the enabled features. The address administration offers an
/// address type when at least one capability is registered for it, and lists that type's capabilities as checkboxes.
/// </summary>
public sealed class OmnichannelAddressOptions
{
    /// <summary>
    /// Gets the registered address types and their localized names, keyed by type (case-insensitive).
    /// </summary>
    public Dictionary<string, OmnichannelAddressType> AddressTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered capabilities, keyed by name (case-insensitive).
    /// </summary>
    public Dictionary<string, OmnichannelAddressCapability> Capabilities { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the capabilities registered for an address type, in registration order.
    /// </summary>
    /// <param name="addressType">The address type.</param>
    public IEnumerable<OmnichannelAddressCapability> GetCapabilities(string addressType)
        => Capabilities.Values.Where(capability => string.Equals(capability.AddressType, addressType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the address types that can be created: those with at least one registered capability.
    /// </summary>
    public IEnumerable<OmnichannelAddressType> GetCreatableTypes()
        => AddressTypes.Values.Where(type => GetCapabilities(type.Name).Any());
}

/// <summary>
/// A kind of address the business can own, such as a phone number.
/// </summary>
public sealed class OmnichannelAddressType
{
    /// <summary>
    /// Gets or sets the type's name (see <see cref="OmnichannelAddressTypes"/>).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the localized name shown for the type.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized description shown when choosing what kind of address to add.
    /// </summary>
    public LocalizedString Description { get; set; }
}
