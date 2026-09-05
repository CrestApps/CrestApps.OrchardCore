using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Voice Contact Center Call Router that routes inbound and outbound voice calls while
/// Telephony providers execute media operations.
/// </summary>
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class VoiceStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind voice ingress options.</param>
    public VoiceStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<ProviderWebhookIngressOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:WebhookIngress"))
            .Validate(
                options => options.ConcurrencyPermitLimit > 0 &&
                    options.ConcurrencyPermitLimit <= 1024 &&
                    options.RatePermitLimit is > 0 and <= 100_000 &&
                    options.RatePeriodSeconds is > 0 and <= 3600 &&
                    options.MaximumDeliveryAgeSeconds is > 0 and <= 86_400 &&
                    options.MaximumFutureSkewSeconds is >= 0 and <= 3600,
                "Webhook ingress rate, concurrency, period, delivery-age, or future-skew values are outside their supported ranges.")
            .ValidateOnStart();

        services
            .AddOptions<BaseVoiceVerificationOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:BaseVoiceVerification"))
            .Validate(
                options => !options.AudioVerificationAcknowledged
                    || !string.IsNullOrWhiteSpace(options.AudioVerificationEvidenceReference),
                "'CrestApps:ContactCenter:BaseVoiceVerification:AudioVerificationAcknowledged' cannot be set without also supplying 'AudioVerificationEvidenceReference', which must point at the retained base-voice acceptance evidence.")
            .ValidateOnStart();

        services.AddScoped<IModularTenantEvents, BaseVoiceVerificationStartupCheck>();

        services
            .AddScoped<IInboundContactLookup, InboundContactLookup>()
            .AddScoped<IInboundVoiceEventSink, InboundVoiceEventSink>()
            .AddScoped<IInboundVoiceInteractionProbe, InboundVoiceInteractionProbe>()
            .AddScoped<IContactCenterVoiceProviderResolver, ContactCenterVoiceProviderResolver>()
            .AddScoped<IContactCenterAgentLegFailureService, ContactCenterAgentLegFailureService>()
            .AddScoped<IContactCenterCallCommandService, ContactCenterCallCommandService>()
            .AddScoped<IProviderCommandStore, ProviderCommandStore>()
            .AddScoped<IProviderCommandManager, ProviderCommandManager>()
            .AddScoped<IContactCenterRetentionPolicy, ProviderCommandRetentionPolicy>()
            .AddScoped<IProviderCommandStateService, ProviderCommandStateService>()
            .AddScoped<IProviderCommandTypeExecutor, DialProviderCommandTypeExecutor>()
            .AddScoped<IProviderCommandTypeExecutor, AnswerProviderCommandTypeExecutor>()
            .AddScoped<IProviderCommandTypeExecutor, RejectProviderCommandTypeExecutor>()
            .AddScoped<IProviderCommandTypeExecutor, SendToVoicemailProviderCommandTypeExecutor>()
            .AddScoped<IProviderCommandProcessor, ProviderCommandProcessor>()
            .AddScoped<IProviderCallStateReconciler, ProviderCallStateReconciler>()
            .AddScoped<IProviderVoiceEventService, ProviderVoiceEventService>()
            .AddScoped<IProviderVoiceEventSink, ProviderVoiceEventSink>()
            .AddScoped<INormalizedVoiceEventHandler, ContactCenterVoiceProjection>()
            .AddScoped<IProviderWebhookInboxHandler, ProviderVoiceEventInboxHandler>()
            .AddScoped<IProviderVoiceOfferSynchronizationService, ProviderVoiceOfferSynchronizationService>()
            .AddSingleton<IProviderWebhookIngressLimiter, ProviderWebhookIngressLimiter>()
            .AddScoped<IContactCenterTransferService, ContactCenterTransferService>()
            .AddScoped<IContactCenterMonitoringService, ContactCenterMonitoringService>()
            .AddScoped<ICallControlAuthorizationService, CallControlAuthorizationService>()
            .AddScoped<ITransferDestinationResolver, TransferDestinationResolver>()
            // Attended transfer as the three phases it is, recorded against the call so a supervisor can see a
            // customer held while their agent talks to somebody else.
            .AddScoped<IConsultTransferService, ConsultTransferService>()
            // With Voice enabled, the soft-phone transfer field resolves through the curated destination catalog
            // rather than accepting whatever an agent types.
            .Replace(ServiceDescriptor.Scoped<ITransferTargetPolicy, ContactCenterTransferTargetPolicy>())
            // Voice is the feature that has a provider to reconcile against, so it replaces the do-nothing
            // default the base feature registers.
            .Replace(ServiceDescriptor.Scoped<IProviderCallStateSynchronizationService, ProviderCallStateSynchronizationService>())
            .AddScoped<IContactCenterEventHandler, ContactCenterVoiceOfferReconciliationHandler>()
            .AddScoped<IContactCenterEventHandler, ReofferVoiceWorkHandler>()
            .AddScoped<IVoiceQueueOfferService, VoiceQueueOfferService>()
            .AddScoped<IDirectHoldTimeoutService, DirectHoldTimeoutService>()
            .AddScoped<IInboundVoiceCallProcessor, InboundVoiceCallProcessor>()
            .AddScoped<IOmnichannelHandoffService, VoiceAgentHandoffService>()
            .AddScoped<VoiceContactCenterCallRouter>()
            .AddScoped<IVoiceContactCenterCallRouter>(sp => sp.GetRequiredService<VoiceContactCenterCallRouter>())
            .AddScoped<IInboundVoiceService>(sp => sp.GetRequiredService<VoiceContactCenterCallRouter>())
            // Inbound routing publishes events, and the publisher constructs the handler that re-offers declined
            // work. The lazy wrapper is what lets that handler declare the dependency instead of fetching it from
            // the container, without the two constructing each other.
            .AddScoped(sp => new Lazy<IInboundVoiceService>(sp.GetRequiredService<IInboundVoiceService>))
            .AddScoped<IIncomingCallContextProvider, ContactCenterIncomingCallContextProvider>()
            .AddScoped<ContactCenterVoiceLifecycleParticipant>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                serviceProvider.GetRequiredService<ContactCenterVoiceLifecycleParticipant>());

        services
            .AddIndexProvider<ProviderCommandIndexProvider>()
            .AddDataMigration<ProviderCommandIndexMigrations>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ProviderCommandRecoveryBackgroundTask>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ProviderCallStateReconciliationBackgroundTask>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddVoiceOfferEndpoints();
    }

}

/// <summary>
/// Registers the Contact Center projection that synchronizes server-side voice state with the Telephony soft phone.
/// This projection is integration glue that activates whenever Contact Center Voice, Contact Center Real-Time, and the
/// Telephony soft phone are all enabled, rather than a separately selectable feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Voice)]
[RequireFeatures(ContactCenterConstants.Feature.RealTime, TelephonyConstants.Feature.SoftPhoneCore)]
public sealed class VoiceSoftPhoneStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterEventHandler, ContactCenterSoftPhoneEventHandler>()
            .AddDisplayDriver<SoftPhoneWidget, ContactCenterSoftPhoneWidgetDisplayDriver>();

        services.AddResourceConfiguration<ContactCenterSoftPhoneResourceConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        routes.AddAgentSoftPhoneEndpoints(adminOptions.AdminUrlPrefix);
    }
}

/// <summary>
/// Registers the health checks owned by the Contact Center Voice feature, but only when the
/// <c>OrchardCore.HealthChecks</c> feature is also enabled so a deployment that does not use health checks never
/// pays for them.
/// </summary>
[Feature(ContactCenterConstants.Feature.Voice)]
[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class VoiceHealthChecksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterVoiceHealthChecks();
    }
}
