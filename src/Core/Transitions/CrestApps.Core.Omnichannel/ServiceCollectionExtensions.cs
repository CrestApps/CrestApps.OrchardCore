using CrestApps.Core.Builders;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Omnichannel;

/// <summary>
/// Turns the omnichannel on, one feature at a time, on the suite builder or directly on an
/// <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// Every builder method here is sugar over the <c>AddCoreOmnichannel*</c> method beneath it. Nothing is
/// registered by asking for the omnichannel itself: a host gets the features it names and nothing else.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the omnichannel to the suite. The features it carries are turned on individually on the builder
    /// this hands back.
    /// </summary>
    /// <param name="builder">The suite builder.</param>
    /// <param name="configure">An optional delegate used to turn on the omnichannel features the host wants.</param>
    /// <returns>The same suite builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterSuiteBuilder AddOmnichannel(
        this CrestAppsContactCenterSuiteBuilder builder,
        Action<CrestAppsOmnichannelBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        configure?.Invoke(new CrestAppsOmnichannelBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Adds the channel-endpoint registry: the addresses a conversation arrives at, and which channel each one
    /// belongs to.
    /// </summary>
    /// <remarks>
    /// The store behind it is a persistence concern a host registers separately, and the channels themselves
    /// are contributed by whichever feature owns them through
    /// <see cref="AddChannelEndpointSource(IServiceCollection, string, Action{ChannelEndpointSource})"/>.
    /// </remarks>
    /// <param name="builder">The omnichannel builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsOmnichannelBuilder AddChannelEndpoints(this CrestAppsOmnichannelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreOmnichannelChannelEndpoints();

        return builder;
    }

    /// <summary>
    /// Adds the automated-conversation services: the tunables a deployment sets, the gate that keeps one reply
    /// in flight per conversation, and the turn a completion records its handoff decision on.
    /// </summary>
    /// <param name="builder">The omnichannel builder.</param>
    /// <param name="configuration">
    /// The configuration section holding the automation tunables, or <see langword="null"/> to take the
    /// defaults.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsOmnichannelBuilder AddAutomation(
        this CrestAppsOmnichannelBuilder builder,
        IConfigurationSection configuration = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreOmnichannelAutomation(configuration);

        return builder;
    }

    /// <summary>
    /// Registers the channel-endpoint registry.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreOmnichannelChannelEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IOmnichannelChannelEndpointManager, OmnichannelChannelEndpointManager>();

        return services;
    }

    /// <summary>
    /// Registers the automated-conversation services.
    /// </summary>
    /// <remarks>
    /// The gate is a singleton inside the container the host gives one tenant, so it is shared by the scoped
    /// handlers separate inbound webhooks create and isolated from every other tenant. The handoff turn is
    /// scoped, so the tool and the handler that ran the completion share one instance and two concurrent
    /// conversations cannot see each other's decision.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <param name="configuration">
    /// The configuration section holding the automation tunables, or <see langword="null"/> to take the
    /// defaults.
    /// </param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreOmnichannelAutomation(
        this IServiceCollection services,
        IConfigurationSection configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configuration is not null)
        {
            services.Configure<OmnichannelAutomationOptions>(configuration);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OmnichannelAutomationOptions>, OmnichannelAutomationOptionsValidator>());
        services.TryAddSingleton<IAutomatedConversationGate, InMemoryAutomatedConversationGate>();
        services.TryAddScoped<IOmnichannelHandoffTurn, OmnichannelHandoffTurn>();

        return services;
    }

    /// <summary>
    /// Registers a channel-endpoint source (a channel such as SMS or Phone). The channel then appears in the
    /// "Add endpoint" picker, and endpoints created for it are edited by the display drivers that target that
    /// channel. Call this from the feature that owns the channel so the source only appears when that feature is
    /// enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="channel">The channel name that also serves as the source key (for example "SMS" or "Phone").</param>
    /// <param name="configure">Configures the source's display name and description.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddChannelEndpointSource(this IServiceCollection services, string channel, Action<ChannelEndpointSource> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
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
