using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering channel-endpoint sources.
/// </summary>
public static class OmnichannelChannelEndpointServiceCollectionExtensions
{
    /// <summary>
    /// Registers a channel-endpoint source (a channel such as SMS or Phone). The channel then appears in the
    /// "Add endpoint" picker, and endpoints created for it are edited by the display drivers that target that
    /// channel. Call this from the feature that owns the channel so the source only appears when that feature is
    /// enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="channel">The channel name that also serves as the source key (for example "SMS" or "Phone").</param>
    /// <param name="configure">Configures the source's display name and description.</param>
    public static IServiceCollection AddChannelEndpointSource(this IServiceCollection services, string channel, Action<ChannelEndpointSource> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<ChannelEndpointSourceOptions>(options =>
        {
            var source = new ChannelEndpointSource();
            configure(source);
            options.Sources[channel] = source;
        });

        return services;
    }
}
