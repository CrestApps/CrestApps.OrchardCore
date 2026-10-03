using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering omnichannel address types and capabilities.
/// </summary>
public static class OmnichannelAddressServiceCollectionExtensions
{
    /// <summary>
    /// Registers a kind of address the business can own. It is offered when adding an address once a capability is
    /// registered for it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="addressType">The address type (see <c>OmnichannelAddressTypes</c>).</param>
    /// <param name="configure">Configures the type's display name and description.</param>
    public static IServiceCollection AddOmnichannelAddressType(this IServiceCollection services, string addressType, Action<OmnichannelAddressType> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(addressType);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<OmnichannelAddressOptions>(options =>
        {
            var type = new OmnichannelAddressType { Name = addressType };
            configure(type);
            options.AddressTypes[addressType] = type;
        });

        return services;
    }

    /// <summary>
    /// Registers something the business can do with an address of a type, such as texting on a phone number. Call it
    /// from the feature that does it, so the capability is offered only while that feature is enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="addressType">The address type the capability applies to.</param>
    /// <param name="capability">The capability's name, which is the channel it serves (for example "SMS" or "Phone").</param>
    /// <param name="configure">Configures the capability's display name and description.</param>
    public static IServiceCollection AddOmnichannelAddressCapability(this IServiceCollection services, string addressType, string capability, Action<OmnichannelAddressCapability> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(addressType);
        ArgumentException.ThrowIfNullOrEmpty(capability);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<OmnichannelAddressOptions>(options =>
        {
            var entry = new OmnichannelAddressCapability
            {
                Name = capability,
                AddressType = addressType,
            };

            configure(entry);
            options.Capabilities[capability] = entry;
        });

        return services;
    }
}
