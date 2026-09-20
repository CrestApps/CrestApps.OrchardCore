using CrestApps.Core.Builders;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Turns the contact centre on, one feature at a time, on the suite builder or directly on an
/// <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every builder method here is sugar over the <c>AddCoreContactCenter*</c> method beneath it. Nothing is
/// registered by asking for the contact centre itself: a host gets the features it names and nothing else.
/// Where those features keep their records is a separate decision, made by calling one of the persistence
/// packages' methods on the same builder.
/// </para>
/// <para>
/// Four of the contact centre's features are not here yet - the base feature, work distribution, agent
/// presence, and the provider inbox. Each of them registers at least one service that takes a YesSql session
/// directly, so registering them from this package would drag a persistence choice in with them. They stay
/// with the host that already made that choice until those services read through a store contract instead.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the contact centre to the suite. The features it carries are turned on individually on the builder
    /// this hands back.
    /// </summary>
    /// <param name="builder">The suite builder.</param>
    /// <param name="configure">An optional delegate used to turn on the contact centre features the host wants.</param>
    /// <returns>The same suite builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterSuiteBuilder AddContactCenter(
        this CrestAppsContactCenterSuiteBuilder builder,
        Action<CrestAppsContactCenterBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        configure?.Invoke(new CrestAppsContactCenterBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Adds the agent directory: who the agents are, what they are entitled to, and which queues they serve.
    /// </summary>
    /// <param name="builder">The contact centre builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterBuilder AddAgentDirectory(this CrestAppsContactCenterBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreContactCenterAgentDirectory();

        return builder;
    }

    /// <summary>
    /// Adds the governance that decides who may reach a recording.
    /// </summary>
    /// <param name="builder">The contact centre builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterBuilder AddRecordingGovernance(this CrestAppsContactCenterBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreContactCenterRecordingGovernance();

        return builder;
    }

    /// <summary>
    /// Adds the resolver that picks which provider plays a piece of voice media.
    /// </summary>
    /// <param name="builder">The contact centre builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterBuilder AddVoiceMedia(this CrestAppsContactCenterBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreContactCenterVoiceMedia();

        return builder;
    }

    /// <summary>
    /// Adds the paced dialing strategies and the sweep that paces them.
    /// </summary>
    /// <param name="builder">The contact centre builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterBuilder AddPacedDialing(this CrestAppsContactCenterBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreContactCenterPacedDialing();

        return builder;
    }

    /// <summary>
    /// Registers the agent directory: the manager over the agent records, the entitlement policy, and the
    /// queue-membership reader expressed over the directory alone.
    /// </summary>
    /// <remarks>
    /// Separate from the agent presence feature because a host that runs only a messaging channel still needs
    /// to know who its agents are, without taking on presence and sessions. The store the manager reads
    /// through is a persistence concern registered separately.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreContactCenterAgentDirectory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAgentProfileManager, AgentProfileManager>();

        // The permissive default: no entitlement restriction. A host that enforces entitlements replaces this.
        // It lives with the directory rather than the agents administration because every consumer of agent
        // identity needs it, including a host that runs only a messaging channel.
        services.TryAddScoped<IAgentEntitlementPolicy, PermissiveAgentEntitlementPolicy>();

        // Queue membership expressed over the agent directory alone, so a channel that groups agents by queue
        // does not need the work-distribution feature to resolve who serves what.
        services.TryAddScoped<IAgentQueueMembershipReader, AgentQueueMembershipReader>();

        return services;
    }

    /// <summary>
    /// Registers the governance that decides who may reach a recording.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreContactCenterRecordingGovernance(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRecordingAccessGovernanceService, RecordingAccessGovernanceService>();

        return services;
    }

    /// <summary>
    /// Registers the resolver that picks which provider plays a piece of voice media.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreContactCenterVoiceMedia(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IContactCenterVoiceMediaProviderResolver, ContactCenterVoiceMediaProviderResolver>();

        return services;
    }

    /// <summary>
    /// Registers the paced dialing strategies and the sweep that paces them.
    /// </summary>
    /// <remarks>
    /// The strategies are selected by the mode a campaign asks for rather than by position, so this is a set
    /// rather than a chain; what matters is that no two of them claim the same mode.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreContactCenterPacedDialing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddScoped<IDialerStrategy, PowerDialerStrategy>()
            .AddScoped<IDialerStrategy, ProgressiveDialerStrategy>()
            // Predictive is not blocked: its pacing is gated by the abandonment policy, which fails closed when
            // the rate cannot be proven.
            .AddScoped<IDialerStrategy, PredictiveDialerStrategy>();

        services.AddBackgroundCycle<IDialerPacingCycle, DialerPacingCycle>();

        return services;
    }
}
