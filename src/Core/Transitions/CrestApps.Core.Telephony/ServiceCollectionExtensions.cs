using CrestApps.Core.Builders;
using CrestApps.Core.Diagnostics;
using CrestApps.Core.Hosting.Background;
using CrestApps.Core.Telephony.Models;
using CrestApps.Core.Telephony.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core.Telephony;

/// <summary>
/// Turns telephony on, one feature at a time, on the suite builder or directly on an
/// <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// Every builder method here is sugar over the <c>AddCoreTelephony*</c> method beneath it. A host composing the
/// suite reads better for using the builder; a host that registers services somewhere the builder does not
/// reach calls the underlying method directly and gets exactly the same registrations. Everything registered
/// here is replaceable: the defaults use <c>TryAdd</c> where a higher-level feature is expected to take over,
/// and a plain <c>Add</c> where it is not.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds telephony to the suite: the options a deployment tunes, the canonical provider identity, and the
    /// redaction rule that keeps phone numbers out of logs. Nothing that places a call is registered until the
    /// host asks for it on the builder this hands back.
    /// </summary>
    /// <param name="builder">The suite builder.</param>
    /// <param name="configuration">
    /// The configuration section holding the telephony settings, with <c>Commands</c> and <c>Coordination</c>
    /// children.
    /// </param>
    /// <param name="configure">An optional delegate used to turn on the telephony features the host wants.</param>
    /// <returns>The same suite builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterSuiteBuilder AddTelephony(
        this CrestAppsContactCenterSuiteBuilder builder,
        IConfigurationSection configuration,
        Action<CrestAppsTelephonyBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.AddCoreTelephony(configuration);

        configure?.Invoke(new CrestAppsTelephonyBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Adds telephony to the suite with the built-in defaults, for a host that configures nothing.
    /// </summary>
    /// <param name="builder">The suite builder.</param>
    /// <param name="configure">An optional delegate used to turn on the telephony features the host wants.</param>
    /// <returns>The same suite builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterSuiteBuilder AddTelephony(
        this CrestAppsContactCenterSuiteBuilder builder,
        Action<CrestAppsTelephonyBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephony();

        configure?.Invoke(new CrestAppsTelephonyBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Adds the provider-neutral voice ingress and the projection that writes what a provider reports into
    /// call history.
    /// </summary>
    /// <param name="builder">The telephony builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddVoiceIngress(this CrestAppsTelephonyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephonyVoiceIngress();

        return builder;
    }

    /// <summary>
    /// Adds the call-placing services: which provider a call goes through, what may be dialed, and what runs
    /// the command.
    /// </summary>
    /// <param name="builder">The telephony builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddCalling(this CrestAppsTelephonyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephonyCalling();

        return builder;
    }

    /// <summary>
    /// Adds how a user authorizes a provider to act on their behalf, and where those tokens are kept.
    /// </summary>
    /// <param name="builder">The telephony builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddAuthentication(this CrestAppsTelephonyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephonyAuthentication();

        return builder;
    }

    /// <summary>
    /// Adds call-history reconciliation: the service that asks a provider what is still live, and the cycle
    /// that sweeps the interactions nobody closed.
    /// </summary>
    /// <remarks>
    /// The cycle is registered without anything to run it, because a host with its own scheduler owns when it
    /// happens. A host with no scheduler calls <see cref="AddInteractionsWorker"/> instead.
    /// </remarks>
    /// <param name="builder">The telephony builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddInteractions(this CrestAppsTelephonyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephonyInteractions();

        return builder;
    }

    /// <summary>
    /// Adds call-history reconciliation together with the runner that drives it on a schedule.
    /// </summary>
    /// <param name="builder">The telephony builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddInteractionsWorker(this CrestAppsTelephonyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephonyInteractionsWorker();

        return builder;
    }

    /// <summary>
    /// Adds the internal extension registry that maps a dialed extension number to an on-platform user.
    /// </summary>
    /// <remarks>
    /// The store this reads through is a persistence concern. A host adds it with <c>AddYesSqlStores</c> on
    /// this builder, or registers its own implementation of <see cref="ITelephonyExtensionStore"/>.
    /// </remarks>
    /// <param name="builder">The telephony builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddExtensions(this CrestAppsTelephonyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephonyExtensions();

        return builder;
    }

    /// <summary>
    /// Adds the notifier that pushes call events to a user's soft phones over the host's hub.
    /// </summary>
    /// <typeparam name="THub">
    /// The host's concrete hub. It carries the host's authorization, which is why the framework never declares
    /// one and takes it as a type argument instead.
    /// </typeparam>
    /// <param name="builder">The telephony builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddSoftPhoneNotifier<THub>(this CrestAppsTelephonyBuilder builder)
        where THub : Hub<ITelephonyClient>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreTelephonySoftPhoneNotifier<THub>();

        return builder;
    }

    /// <summary>
    /// Adds the encrypted recording store over a directory on disk.
    /// </summary>
    /// <param name="builder">The telephony builder.</param>
    /// <param name="rootPathResolver">
    /// Resolves the directory the recordings are written to. It is resolved per host rather than fixed, which
    /// is how a host that serves more than one tenant gives each of them its own directory.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsTelephonyBuilder AddLocalRecordingStore(
        this CrestAppsTelephonyBuilder builder,
        Func<IServiceProvider, string> rootPathResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(rootPathResolver);

        builder.Services.AddCoreTelephonyLocalRecordingStore(rootPathResolver);

        return builder;
    }

    /// <summary>
    /// Registers the telephony core with the built-in defaults, for a host that configures nothing.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephony(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddCoreTelephony(configuration: null);
    }

    /// <summary>
    /// Registers the telephony core: the options a deployment tunes, the canonical provider identity, and the
    /// redaction rule that keeps phone numbers out of logs.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">
    /// The configuration section holding the telephony settings, with <c>Commands</c> and <c>Coordination</c>
    /// children, or <see langword="null"/> to take the defaults.
    /// </param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephony(this IServiceCollection services, IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        var commands = services.AddOptions<TelephonyCommandOptions>();

        if (configuration is not null)
        {
            commands.Bind(configuration.GetSection("Commands"));
        }

        commands
            .Validate(
                options => options.Timeout >= TimeSpan.FromSeconds(TelephonyCommandOptions.MinimumTimeoutSeconds) &&
                    options.Timeout <= TimeSpan.FromSeconds(TelephonyCommandOptions.MaximumTimeoutSeconds),
                "The Telephony command timeout must be between one second and two minutes.")
            .ValidateOnStart();

        var coordination = services.AddOptions<TelephonyCoordinationOptions>();

        if (configuration is not null)
        {
            coordination.Bind(configuration.GetSection("Coordination"));
        }

        coordination
            .Validate(
                options => options.InteractionLockTimeout > TimeSpan.Zero,
                "'CrestApps_Telephony:Coordination:InteractionLockTimeout' must be greater than zero.")
            .Validate(
                options => options.InteractionLockExpiration > options.InteractionLockTimeout,
                "'CrestApps_Telephony:Coordination:InteractionLockExpiration' must exceed 'InteractionLockTimeout', otherwise the reconciliation lease expires while a peer is still waiting for it and two sweeps run at once.")
            .Validate(
                options => options.NewInteractionGracePeriod > TimeSpan.Zero,
                "'CrestApps_Telephony:Coordination:NewInteractionGracePeriod' must be greater than zero, otherwise reconciliation can terminate an interaction another node has only just written.")
            .Validate(
                options => options.ClientRecordedCallMaxAge > options.NewInteractionGracePeriod,
                "'CrestApps_Telephony:Coordination:ClientRecordedCallMaxAge' must exceed 'NewInteractionGracePeriod', otherwise reconciliation can disconnect a browser-originated call that is still in progress.")
            .Validate(
                options => options.TokenRefreshLockTimeout > TimeSpan.Zero,
                "'CrestApps_Telephony:Coordination:TokenRefreshLockTimeout' must be greater than zero.")
            .Validate(
                options => options.TokenRefreshLockExpiration > options.TokenRefreshLockTimeout,
                "'CrestApps_Telephony:Coordination:TokenRefreshLockExpiration' must exceed 'TokenRefreshLockTimeout', otherwise the refresh lease expires while a peer is still waiting for it and two refreshes run at once.")
            .ValidateOnStart();

        services.TryAddSingleton<IProviderIdentityResolver, ProviderIdentityResolver>();

        // Everything that records what a call did announces it, whether or not a soft phone is listening.
        // The push target is therefore a core service with a silent default rather than something only the
        // soft-phone feature registers, so a host that wants call history and no soft phone still resolves.
        services.TryAddScoped<ITelephonySoftPhoneNotifier, NullTelephonySoftPhoneNotifier>();

        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));

        return services;
    }

    /// <summary>
    /// Registers the provider-neutral voice ingress and the projection that writes what a provider reports
    /// into call history.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyVoiceIngress(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IVoiceIngressGate, VoiceIngressGate>();
        services.AddScoped<INormalizedVoiceEventIngestor, NormalizedVoiceEventIngestor>();
        // A handler chain, so the projection is added rather than replacing whatever else a host or a
        // provider contributed, and registering the core twice does not run it twice.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<INormalizedVoiceEventHandler, TelephonyCallHistoryVoiceEventHandler>());

        return services;
    }

    /// <summary>
    /// Registers the call-placing services: which provider a call goes through, what may be dialed, and what
    /// runs the command.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyCalling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITelephonyProviderResolver, DefaultTelephonyProviderResolver>();
        services.AddScoped<IVoiceAgentMediaProviderResolver, VoiceAgentMediaProviderResolver>();
        services.AddScoped<IOutboundCallScreeningService, DefaultOutboundCallScreeningService>();

        // The destination safety policy. A contact center decorates it with its approved-catalog rules; the
        // default here refuses emergency and premium destinations for every telephony consumer.
        services.TryAddScoped<IDialDestinationPolicy, DefaultDialDestinationPolicy>();

        // What an agent may transfer to. A contact center replaces this with a policy that accepts only
        // curated destinations, so the soft phone transfer field stops accepting a raw number there.
        services.TryAddScoped<ITransferTargetPolicy, DefaultTransferTargetPolicy>();

        services.AddScoped<ITelephonyService, DefaultTelephonyService>();
        services.AddScoped<ITelephonyCommandExecutor, DefaultTelephonyCommandExecutor>();
        services.AddScoped<IIncomingCallDispatcher, DefaultIncomingCallDispatcher>();

        return services;
    }

    /// <summary>
    /// Registers how a user authorizes a provider to act on their behalf, and where those tokens are kept.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITelephonyUserTokenStore, DefaultTelephonyUserTokenStore>();
        services.AddScoped<ITelephonyAuthenticationService, DefaultTelephonyAuthenticationService>();

        return services;
    }

    /// <summary>
    /// Registers the call-history reconciliation: the service that asks a provider what is still live, and the
    /// cycle that sweeps the interactions nobody closed.
    /// </summary>
    /// <remarks>
    /// The cycle is registered without a runner, because a host with its own scheduler owns when it happens. A
    /// host with no scheduler calls <see cref="AddCoreTelephonyInteractionsWorker"/> instead.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyInteractions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITelephonyInteractionSynchronizationService, TelephonyInteractionSynchronizationService>();
        services.AddBackgroundCycle<ITelephonyInteractionReconciliationCycle, TelephonyInteractionReconciliationCycle>();

        return services;
    }

    /// <summary>
    /// Registers the call-history reconciliation together with the runner that drives it on a schedule.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyInteractionsWorker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITelephonyInteractionSynchronizationService, TelephonyInteractionSynchronizationService>();
        services.AddBackgroundCycleWorker<ITelephonyInteractionReconciliationCycle, TelephonyInteractionReconciliationCycle>();

        return services;
    }

    /// <summary>
    /// Registers the internal extension registry: the provider-neutral system of record that maps a dialed
    /// extension number to an on-platform user. Providers translate the resolved user into their own live
    /// endpoint.
    /// </summary>
    /// <remarks>
    /// The store this reads through is a persistence concern, registered separately by whichever store package
    /// the host uses.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyExtensions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITelephonyExtensionManager, TelephonyExtensionManager>();
        services.AddScoped<ITelephonyExtensionResolver, TelephonyExtensionResolver>();

        return services;
    }

    /// <summary>
    /// Registers the notifier that pushes call events to a user's soft phones over the host's hub.
    /// </summary>
    /// <typeparam name="THub">
    /// The host's concrete hub. It carries the host's authorization, which is why the framework never declares
    /// one and takes it as a type argument instead.
    /// </typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonySoftPhoneNotifier<THub>(this IServiceCollection services)
        where THub : Hub<ITelephonyClient>
    {
        ArgumentNullException.ThrowIfNull(services);

        // Replaces rather than adds, because the core already registered the silent default and a
        // second registration would leave two descriptors where the last one silently decides.
        services.Replace(ServiceDescriptor.Scoped<ITelephonySoftPhoneNotifier, TelephonySoftPhoneNotifier<THub>>());

        return services;
    }

    /// <summary>
    /// Registers the encrypted recording store over a directory on disk.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="rootPathResolver">
    /// Resolves the directory the recordings are written to. It is resolved per host rather than fixed, which
    /// is how a host that serves more than one tenant gives each of them its own directory, and that is what
    /// keeps one tenant's recordings from being addressable by another.
    /// </param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyLocalRecordingStore(
        this IServiceCollection services,
        Func<IServiceProvider, string> rootPathResolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rootPathResolver);

        services.AddSingleton<IRecordingMediaStore>(serviceProvider => new LocalEncryptedRecordingMediaStore(
            new LocalRecordingMediaFileStore(rootPathResolver(serviceProvider)),
            serviceProvider.GetRequiredService<IDataProtectionProvider>()));

        return services;
    }
}
