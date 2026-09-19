using CrestApps.Core.Hosting.Background;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core;

/// <summary>
/// Registers the Contact Center's services, independently of the host they run in.
/// </summary>
public static class ContactCenterServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Contact Center's options, its interaction and metric services, its retention policies and
    /// its periodic work.
    /// </summary>
    /// <remarks>
    /// What stays with the host is everything only a host can answer: where its documents are stored, how its
    /// indexes and migrations are registered, what its administration screens look like, and the scheduler
    /// that drives the cycles added here.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration the options are bound against.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreContactCenter(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ContactCenterRetentionOptions>()
            .Bind(configuration.GetSection("CrestApps:ContactCenter:Retention"))
            .Validate(
                options => options.InteractionEventRetentionDays >= 0,
                "'CrestApps:ContactCenter:Retention:InteractionEventRetentionDays' cannot be negative. Use zero to keep interaction events indefinitely.")
            .Validate(
                options => options.ProjectionReplayHorizonDays >= 0,
                "'CrestApps:ContactCenter:Retention:ProjectionReplayHorizonDays' cannot be negative. Use zero to apply no replay floor.")
            .Validate(
                options => options.LegalHoldMinimumDays >= 0,
                "'CrestApps:ContactCenter:Retention:LegalHoldMinimumDays' cannot be negative. Use zero to apply no legal-hold floor.")
            .Validate(
                options => options.InteractionRetentionDays >= 0
                    && options.CallSessionRetentionDays >= 0
                    && options.QueueItemRetentionDays >= 0
                    && options.ActivityReservationRetentionDays >= 0
                    && options.OutboxMessageRetentionDays >= 0
                    && options.WebhookInboxMessageRetentionDays >= 0
                    && options.ProviderCommandRetentionDays >= 0
                    && options.AgentSessionRetentionDays >= 0
                    && options.CallbackRequestRetentionDays >= 0
                    && options.EventMetricRetentionDays >= 0
                    && options.SecureCaptureRetentionDays >= 0
                    && options.ProcessedEventRetentionDays >= 0
                    && options.WorkStateRetentionDays >= 0,
                "Every 'CrestApps:ContactCenter:Retention' window must be zero or greater. Use zero to keep that entity indefinitely.")
            .Validate(
                options => options.ProcessedEventDeliveryEnvelopeDays >= 0,
                "'CrestApps:ContactCenter:Retention:ProcessedEventDeliveryEnvelopeDays' cannot be negative. Use zero to apply no redelivery floor.")
            .Validate(
                options => options.PurgeBatchSize >= 0,
                "'CrestApps:ContactCenter:Retention:PurgeBatchSize' cannot be negative. Use zero to apply the default batch size.")
            .Validate(
                options => options.MaxPurgeBatchesPerCycle >= 0,
                "'CrestApps:ContactCenter:Retention:MaxPurgeBatchesPerCycle' cannot be negative. Use zero to apply the default batch budget.")
            .ValidateOnStart();

        services
            .AddOptions<ContactCenterCoordinationOptions>()
            .Bind(configuration.GetSection("CrestApps:ContactCenter:Coordination"))
            .Validate(
                options => options.InboundLockTimeout > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Coordination:InboundLockTimeout' must be greater than zero.")
            .Validate(
                options => options.InboundLockExpiration > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Coordination:InboundLockExpiration' must be greater than zero.")
            .ValidateOnStart();

        // Every coordination timing is validated in one place so a value that would never acquire a lock, or
        // would let a second node take work the first is still doing, fails at startup naming the key.
        services.AddSingleton<IValidateOptions<ContactCenterCoordinationOptions>, ContactCenterCoordinationOptionsValidator>();

        services
            .AddOptions<ContactCenterTopologyOptions>()
            .Bind(configuration.GetSection("CrestApps:ContactCenter:Topology"))
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ProfileId)
                    || ContactCenterTopologyProfiles.Find(options.ProfileId) is not null,
                $"'CrestApps:ContactCenter:Topology:ProfileId' is not recognized. Recognized profiles are: {string.Join(", ", ContactCenterTopologyProfiles.All.Select(profile => profile.Id).Order(StringComparer.Ordinal))}.")
            .ValidateOnStart();

        services.AddSingleton<ContactCenterTopologyState>();

        // Defaults for the contracts optional features implement. They are registered here, in the feature that
        // declares them, so every consumer can take the contract as a plain constructor parameter instead of
        // scanning the container for it; the owning feature replaces its own. TryAdd plus Replace is order-safe,
        // which a bare AddScoped in both places would not be.
        services.TryAddScoped<ICallbackService, NoCallbackService>();
        services.TryAddScoped<IAgentWorkStateHealingService, NoAgentWorkStateHealingService>();
        services.TryAddScoped<IQueuedVoiceWorkOfferService, NoQueuedVoiceWorkOfferService>();
        services.TryAddScoped<IDialerProfileReader, NullDialerProfileReader>();
        services.TryAddScoped<IBusinessHoursGate, AlwaysOpenBusinessHoursGate>();

        // The entry-point chain asks every registered resolver in turn. It lives here rather than in the
        // inbound feature because the inbound processor is constructed on tenants that have no resolvers at
        // all, and a chain over nothing is a valid chain that resolves nothing.
        services.TryAddScoped<EntryPointResolverChain>();

        services.TryAddScoped<IProviderCallStateSynchronizationService, NoProviderCallStateSynchronizationService>();

        // Healing declares its synchronization dependency but resolves it lazily, because presence constructs
        // healing and synchronization ends up back at presence.
        services.TryAddScoped(sp => new Lazy<IProviderCallStateSynchronizationService>(sp.GetRequiredService<IProviderCallStateSynchronizationService>));

        services
            .AddOptions<ContactCenterFeatureLifecycleOptions>()
            .Bind(configuration.GetSection("CrestApps:ContactCenter:FeatureLifecycle"))
            .Validate(
                options => options.DrainTimeoutSeconds is >= 1 and <= 300,
                "The Contact Center feature drain timeout must be between 1 and 300 seconds.")
            .ValidateOnStart();

        services
            .AddScoped<IInteractionStore, InteractionStore>()
            .AddScoped<IInteractionManager, InteractionManager>()
            .AddScoped<IInteractionEventStore, InteractionEventStore>()
            .AddScoped<IInteractionEventUpcastService, DefaultInteractionEventUpcastService>()
            .AddScoped<IContactCenterOutboxStore, ContactCenterOutboxStore>()
            .AddScoped<IContactCenterOutbox, ContactCenterOutbox>()
            .AddScoped<IContactCenterWorkStateActivityProjection, ContactCenterWorkStateActivityProjection>()
            .AddScoped<IContactCenterActivityWriter, ContactCenterActivityWriter>()
            .AddScoped<ContactCenterEventDispatchContext>()
            .AddScoped<IContactCenterEventPublisher, DefaultContactCenterEventPublisher>()
            .AddScoped<IContactCenterMetricStore, ContactCenterMetricStore>()
            .AddScoped<IContactCenterMetricDeltaStore, ContactCenterMetricDeltaStore>()
            .AddScoped<IContactCenterMetricRollupService, ContactCenterMetricRollupService>()
            .AddScoped<IContactCenterMetricsService, ContactCenterMetricsService>()
            .AddScoped<IContactCenterProjectionCheckpointStore, ContactCenterProjectionCheckpointStore>()
            .AddScoped<IContactCenterMetricsProjectionMaintenanceService, ContactCenterMetricsProjectionMaintenanceService>()
            .AddScoped<IContactCenterEventDeduplicationService, ContactCenterEventDeduplicationService>()
            .AddScoped<IContactCenterProcessedEventStore, ContactCenterProcessedEventStore>()
            .AddScoped<IContactCenterRetentionService, ContactCenterRetentionService>()
            .AddScoped<IContactCenterRetentionPolicy, InteractionEventRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, InteractionRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, CallSessionRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterOutboxMessageRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterEventMetricRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterEventMetricDeltaRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterProcessedEventRetentionPolicy>()
            .AddScoped<IContactCenterAssistService, ContactCenterAssistService>();

        // The reusable voice media library (hold music, greetings, prompts) referenced by queues, campaigns, and
        // entry points. Registered in the base feature so the library is available wherever those are configured.
        services
            .AddScoped<IVoiceMediaItemStore, VoiceMediaItemStore>()
            .AddScoped<IVoiceMediaItemManager, VoiceMediaItemManager>();

        services
            .AddScoped<ICallSessionStore, CallSessionStore>()
            .AddScoped<ICallSessionManager, CallSessionManager>();

        // Routing owns assignment and reservation state in its own document so that a routing transition never
        // contends with a CRM edit of the same activity row.
        services
            .AddScoped<IContactCenterWorkStateStore, ContactCenterWorkStateStore>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterWorkStateRetentionPolicy>()
            .AddScoped<IContactCenterWorkStateManager, ContactCenterWorkStateManager>()
            .AddScoped<IContactCenterWorkStateService, ContactCenterWorkStateService>();

        // The call-session index and its migration canonicalize provider identity, and this feature does not
        // depend on Telephony, so the resolver must also be available without the Telephony module.
        services.TryAddSingleton<IProviderIdentityResolver, ProviderIdentityResolver>();

        services.AddBackgroundCycle<IOutboxDispatchCycle, OutboxDispatchCycle>();
        services.AddBackgroundCycle<IContactCenterRetentionCycle, ContactCenterRetentionCycle>();
        services.AddBackgroundCycle<IContactCenterMetricRollupCycle, ContactCenterMetricRollupCycle>();

        return services;
    }
}
