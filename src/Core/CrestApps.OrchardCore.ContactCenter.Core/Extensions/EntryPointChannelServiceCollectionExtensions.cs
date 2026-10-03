using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the channels inbound entry points can answer.
/// </summary>
public static class EntryPointChannelServiceCollectionExtensions
{
    /// <summary>
    /// Registers a channel inbound entry points can answer. Call it from the feature that answers it, so the channel is
    /// offered only while that feature is enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="channel">The address capability the channel answers (for example "Phone" or "SMS").</param>
    /// <param name="configure">Configures the channel's display name and description.</param>
    public static IServiceCollection AddEntryPointChannel(this IServiceCollection services, string channel, Action<EntryPointChannel> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<EntryPointChannelOptions>(options =>
        {
            var entry = new EntryPointChannel { Name = channel };
            configure(entry);
            options.Channels[channel] = entry;
        });

        return services;
    }
}
