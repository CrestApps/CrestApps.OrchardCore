using CrestApps.Core.Diagnostics;
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
/// Registers the telephony services a host needs to place, control and record calls.
/// </summary>
/// <remarks>
/// A host calls these instead of naming the implementations itself, so the wiring is one thing rather than one
/// thing per host. Everything registered here is replaceable: the defaults use <c>TryAdd</c> where a
/// higher-level feature is expected to take over, and a plain <c>Add</c> where it is not.
/// </remarks>
public static class CoreTelephonyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the telephony core: the options a deployment tunes, the canonical provider identity, and the
    /// redaction rule that keeps phone numbers out of logs.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">
    /// The configuration section holding the telephony settings, with <c>Commands</c> and <c>Coordination</c>
    /// children. A host that configures nothing passes an empty section and gets the defaults.
    /// </param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephony(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<TelephonyCommandOptions>()
            .Bind(configuration.GetSection("Commands"))
            .Validate(
                options => options.Timeout >= TimeSpan.FromSeconds(TelephonyCommandOptions.MinimumTimeoutSeconds) &&
                    options.Timeout <= TimeSpan.FromSeconds(TelephonyCommandOptions.MaximumTimeoutSeconds),
                "The Telephony command timeout must be between one second and two minutes.")
            .ValidateOnStart();

        services
            .AddOptions<TelephonyCoordinationOptions>()
            .Bind(configuration.GetSection("Coordination"))
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
        services.AddScoped<INormalizedVoiceEventHandler, TelephonyCallHistoryVoiceEventHandler>();

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

        services.AddScoped<ITelephonySoftPhoneNotifier, TelephonySoftPhoneNotifier<THub>>();

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
