using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers messaging channels with the workspace.
/// </summary>
public static class MessagingChannelServiceCollectionExtensions
{
    /// <summary>
    /// Adds a channel the messaging workspace can send and receive on. Call it from the feature that provides the
    /// channel; the workspace lists, routes and renders conversations for every channel registered this way.
    /// </summary>
    /// <typeparam name="TChannel">The channel implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMessagingChannel<TChannel>(this IServiceCollection services)
        where TChannel : class, IMessagingChannel
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TChannel>();
        services.AddScoped<IMessagingChannel>(sp => sp.GetRequiredService<TChannel>());

        return services;
    }
}
