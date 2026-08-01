using CrestApps.OrchardCore.Asterisk.BackgroundTasks;
using CrestApps.OrchardCore.Asterisk.Drivers;
using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Migrations;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Configuration;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Extensions;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using Polly;

namespace CrestApps.OrchardCore.Asterisk;

/// <summary>
/// Registers the Asterisk telephony providers and their settings driver.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind the Asterisk options.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // The resilience pipeline is constructed before any tenant request, so its timings are read here rather
        // than resolved per call. The same section backs AsteriskCoordinationOptions below, so the validated
        // values and the values the pipeline uses cannot diverge.
        var coordination = new AsteriskCoordinationOptions();
        _shellConfiguration.GetSection(AsteriskConstants.CoordinationConfigurationSectionPath).Bind(coordination);

        services.AddHttpClient(AsteriskConstants.HttpClientName)
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = coordination.HttpTotalRequestTimeout;
                options.AttemptTimeout.Timeout = coordination.HttpAttemptTimeout;

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                options.CircuitBreaker.FailureRatio = 0.1;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 100;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });

        services.ValidateTenantOptionsOnActivation();
        services.AddOptions<DefaultAsteriskOptions>().ValidateOnStart();

        services
            .AddOptions<AsteriskCoordinationOptions>()
            .Bind(_shellConfiguration.GetSection(AsteriskConstants.CoordinationConfigurationSectionPath))
            .Validate(
                options => options.CredentialLockTimeout > TimeSpan.Zero,
                "'CrestApps:Asterisk:Coordination:CredentialLockTimeout' must be greater than zero.")
            .Validate(
                options => options.CredentialLockExpiration > options.CredentialLockTimeout,
                "'CrestApps:Asterisk:Coordination:CredentialLockExpiration' must exceed 'CredentialLockTimeout', otherwise the lease expires while a peer is still waiting for it and two nodes issue credentials for the same endpoint.")
            .Validate(
                options => options.PendingReclamationThreshold > TimeSpan.Zero,
                "'CrestApps:Asterisk:Coordination:PendingReclamationThreshold' must be greater than zero, otherwise reconciliation reclaims a call that is still being answered.")
            .Validate(
                options => options.HttpAttemptTimeout > TimeSpan.Zero,
                "'CrestApps:Asterisk:Coordination:HttpAttemptTimeout' must be greater than zero.")
            .Validate(
                options => options.HttpTotalRequestTimeout > options.HttpAttemptTimeout,
                "'CrestApps:Asterisk:Coordination:HttpTotalRequestTimeout' must exceed 'HttpAttemptTimeout', otherwise no attempt can complete within the total budget.")
            .Validate(
                options => options.RealtimeEventBufferCapacity > 0,
                "'CrestApps:Asterisk:Coordination:RealtimeEventBufferCapacity' must be greater than zero, otherwise the real-time receive loop has nowhere to buffer provider events.")
            .Validate(
                options => options.RealtimeEventBufferCapacity <= AsteriskConstants.MaxRealtimeEventBufferCapacity,
                $"'CrestApps:Asterisk:Coordination:RealtimeEventBufferCapacity' must not exceed {AsteriskConstants.MaxRealtimeEventBufferCapacity}, otherwise a saturated buffer can grow large enough to exhaust process memory before backpressure engages.")
            .Validate(
                options => options.RealtimeEventBackpressureTimeout > TimeSpan.Zero,
                "'CrestApps:Asterisk:Coordination:RealtimeEventBackpressureTimeout' must be greater than zero, otherwise a saturated buffer reconnects immediately instead of applying backpressure.")
            .ValidateOnStart();

        services
            .AddTelephonyProviderOptionsConfiguration<AsteriskProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<AsteriskSettingsDisplayDriver>()
            .AddTransient<IConfigureOptions<DefaultAsteriskOptions>, DefaultAsteriskOptionsConfiguration>()
            .AddTransient<IValidateOptions<DefaultAsteriskOptions>, DefaultAsteriskOptionsValidator>()
            .AddScoped<IAsteriskPjsipCredentialIssuer, AsteriskPjsipCredentialIssuer>()
            .AddScoped<IAsteriskPjsipRealtimeCredentialStore, AsteriskPjsipRealtimeCredentialStore>()
            .AddScoped<IAsteriskPjsipCredentialLeaseStore, AsteriskPjsipCredentialLeaseStore>()
            .AddScoped<IAsteriskPjsipDialogTerminator, AsteriskPjsipDialogTerminator>()
            .AddScoped<ISoftPhoneRegistrationConfigContributor, AsteriskSoftPhoneRegistrationConfigContributor>()
            .AddScoped<ISoftPhoneCredentialRevoker, AsteriskSoftPhoneCredentialRevoker>();

        services.AddIndexProvider<AsteriskPjsipCredentialLeaseIndexProvider>();
        services.AddDataMigration<AsteriskPjsipCredentialLeaseMigrations>();

        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));

        services.AddSingleton<IBackgroundTask, AsteriskPjsipCredentialCleanupBackgroundTask>();

        services
            .AddSingleton<IAsteriskAriApplicationOwnershipRegistry, AsteriskAriApplicationOwnershipRegistry>()
            .AddSingleton<IAsteriskAriApplicationGate, AsteriskAriApplicationGate>()
            .AddSingleton<IAsteriskRealtimeVoiceListener, AsteriskRealtimeVoiceListener>()
            .AddScoped<AsteriskRealtimeVoiceEventDispatcher>()
            .AddScoped<IAsteriskProviderStateReconciler, AsteriskTelephonyProviderStateReconciler>()
            .AddScoped<IModularTenantEvents, AsteriskRealtimeVoiceTenantEvents>();
    }
}

/// <summary>
/// Registers the Asterisk Contact Center voice adapter.
/// </summary>
[Feature(AsteriskConstants.Feature.ContactCenterVoice)]
public sealed class AsteriskContactCenterVoiceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterVoiceProvider, AsteriskContactCenterVoiceProvider>()
            .AddSingleton<IProviderIdentityProvider, AsteriskProviderIdentityProvider>()
            .AddSingleton<IAsteriskAgentChannelReadySignal, AsteriskAgentChannelReadySignal>()
            .AddScoped<IAsteriskRealtimeVoiceEventBridge, AsteriskAgentChannelReadyBridge>()
            .AddScoped<IAsteriskCallTeardownService, AsteriskCallTeardownService>()
            .AddScoped<IAsteriskRealtimeVoiceEventBridge, AsteriskInboundCallOfferBridge>()
            .AddScoped<IAsteriskProviderStateReconciler, AsteriskInboundReconciler>()
            .AddScoped<IAsteriskProviderStateReconciler, AsteriskContactCenterProviderStateReconciler>()
            .AddScoped<IAsteriskAriClient, AsteriskAriClient>()
            .AddScoped<IAsteriskChannelTenantBindingStore, AsteriskChannelTenantBindingStore>()
            .AddScoped<IAsteriskChannelOwnershipGuard, AsteriskChannelOwnershipGuard>()
            .AddScoped<IAsteriskRecordingIngestJobStore, AsteriskRecordingIngestJobStore>()
            .AddScoped<IAsteriskRecordingIngestService, AsteriskRecordingIngestService>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new AsteriskContactCenterFeatureLifecycleParticipant(
                    AsteriskConstants.Feature.ContactCenterVoice,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        services.AddIndexProvider<AsteriskChannelTenantBindingIndexProvider>();
        services.AddDataMigration<AsteriskChannelTenantBindingMigrations>();
        services.AddIndexProvider<AsteriskRecordingIngestJobIndexProvider>();
        services.AddDataMigration<AsteriskRecordingIngestJobMigrations>();
        services.AddSingleton<IBackgroundTask, AsteriskInboundReconciliationBackgroundTask>();
        services.AddSingleton<IBackgroundTask, AsteriskRecordingIngestBackgroundTask>();
    }
}

/// <summary>
/// Registers Asterisk bidirectional RTP media for Contact Center voice calls.
/// </summary>
[Feature(AsteriskConstants.Feature.ContactCenterMedia)]
public sealed class AsteriskContactCenterMediaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterVoiceMediaProvider, AsteriskContactCenterVoiceMediaProvider>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new AsteriskContactCenterFeatureLifecycleParticipant(
                    AsteriskConstants.Feature.ContactCenterMedia,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));
    }
}
