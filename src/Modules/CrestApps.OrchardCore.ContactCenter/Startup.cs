using CrestApps.Core.Services;
using CrestApps.OrchardCore.Configuration;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the services and configuration for the base Contact Center feature.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind Contact Center options.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterStorage.CollectionName));

        services.ValidateTenantOptionsOnActivation();

        services
            .AddOptions<ContactCenterRetentionOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Retention"))
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
            .AddOptions<ContactCenterHealthCheckOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:HealthChecks"))
            .Validate(
                options => options.DeadLetterDegradedThreshold >= 1,
                "'CrestApps:ContactCenter:HealthChecks:DeadLetterDegradedThreshold' must be at least one.")
            .Validate(
                options => options.OverdueBacklogDegradedThreshold >= 1,
                "'CrestApps:ContactCenter:HealthChecks:OverdueBacklogDegradedThreshold' must be at least one.")
            .Validate(
                options => options.ConsecutiveFailuresBeforeUnready >= 1,
                "'CrestApps:ContactCenter:HealthChecks:ConsecutiveFailuresBeforeUnready' must be at least one.")
            .Validate(
                options => options.ConsecutiveSuccessesBeforeReady >= 1,
                "'CrestApps:ContactCenter:HealthChecks:ConsecutiveSuccessesBeforeReady' must be at least one.")
            .Validate(
                options => options.DeadLetterUnhealthyThreshold >= options.DeadLetterDegradedThreshold,
                "'CrestApps:ContactCenter:HealthChecks:DeadLetterUnhealthyThreshold' cannot be lower than 'DeadLetterDegradedThreshold'.")
            .Validate(
                options => options.OverdueBacklogUnhealthyThreshold >= options.OverdueBacklogDegradedThreshold,
                "'CrestApps:ContactCenter:HealthChecks:OverdueBacklogUnhealthyThreshold' cannot be lower than 'OverdueBacklogDegradedThreshold'.")
            .ValidateOnStart();

        services
            .AddOptions<ContactCenterCoordinationOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Coordination"))
            .Validate(
                options => options.InboundLockTimeout > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Coordination:InboundLockTimeout' must be greater than zero.")
            .Validate(
                options => options.InboundLockExpiration > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Coordination:InboundLockExpiration' must be greater than zero.")
            .Validate(
                options => options.InboundLockExpiration > options.InboundLockTimeout,
                "'CrestApps:ContactCenter:Coordination:InboundLockExpiration' must exceed 'InboundLockTimeout', otherwise the lease expires while a peer is still waiting for it and two nodes route the same call.")
            .ValidateOnStart();

        services
            .AddOptions<ContactCenterTopologyOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Topology"))
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ProfileId)
                    || ContactCenterTopologyProfiles.Find(options.ProfileId) is not null,
                $"'CrestApps:ContactCenter:Topology:ProfileId' is not recognized. Recognized profiles are: {string.Join(", ", ContactCenterTopologyProfiles.All.Select(profile => profile.Id).Order(StringComparer.Ordinal))}.")
            .ValidateOnStart();

        services.AddSingleton<ContactCenterTopologyState>();
        services.AddScoped<IModularTenantEvents, ContactCenterTopologyValidator>();
        services
            .AddOptions<ContactCenterFeatureLifecycleOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:FeatureLifecycle"))
            .Validate(
                options => options.DrainTimeoutSeconds is >= 1 and <= 300,
                "The Contact Center feature drain timeout must be between 1 and 300 seconds.")
            .ValidateOnStart();

        services
            .AddScoped<ContactCenterFeatureLifecycleCoordinator>()
            .AddScoped<IFeatureEventHandler, ContactCenterFeatureLifecycleHandler>()
            .AddSingleton<IContactCenterFeatureWorkManager, ContactCenterFeatureWorkManager>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterConstants.Feature.Area,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IInteractionStore, InteractionStore>()
            .AddScoped<IInteractionManager, InteractionManager>()
            .AddScoped<IInteractionEventStore, InteractionEventStore>()
            .AddScoped<IInteractionEventUpcastService, DefaultInteractionEventUpcastService>()
            .AddScoped<IContactCenterOutboxStore, ContactCenterOutboxStore>()
            .AddScoped<IContactCenterOutbox, ContactCenterOutbox>()
            .AddScoped<IContactCenterScopeExecutor, ContactCenterScopeExecutor>()
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
            .AddScoped<IContactCenterEventHandler, ContactCenterMetricsProjectionHandler>()
            .AddScoped<IContactCenterProcessedEventStore, ContactCenterProcessedEventStore>()
            .AddScoped<IContactCenterRetentionService, ContactCenterRetentionService>()
            .AddScoped<IContactCenterRetentionPolicy, InteractionEventRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, InteractionRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, CallSessionRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterOutboxMessageRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterEventMetricRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterEventMetricDeltaRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterProcessedEventRetentionPolicy>()
            .AddScoped<IContactCenterAssistService, ContactCenterAssistService>()
            .AddScoped<ICatalogEntryHandler<Interaction>, InteractionHandler>();

        services
            .AddIndexProvider<ContactCenterEventMetricIndexProvider>()
            .AddDataMigration<ContactCenterEventMetricIndexMigrations>()
            .AddIndexProvider<ContactCenterEventMetricDeltaIndexProvider>()
            .AddDataMigration<ContactCenterEventMetricDeltaIndexMigrations>();

        services
            .AddIndexProvider<ContactCenterProcessedEventIndexProvider>()
            .AddDataMigration<ContactCenterProcessedEventIndexMigrations>();

        services
            .AddIndexProvider<ContactCenterProjectionCheckpointIndexProvider>()
            .AddDataMigration<ContactCenterProjectionCheckpointIndexMigrations>();

        services
            .AddScoped<ICallSessionStore, CallSessionStore>()
            .AddScoped<ICallSessionManager, CallSessionManager>();

        services
            .AddIndexProvider<InteractionIndexProvider>()
            .AddDataMigration<InteractionIndexMigrations>();

        services
            .AddIndexProvider<InteractionEventIndexProvider>()
            .AddDataMigration<InteractionEventIndexMigrations>();

        services
            .AddIndexProvider<ContactCenterOutboxMessageIndexProvider>()
            .AddDataMigration<ContactCenterOutboxMessageIndexMigrations>();

        // Routing owns assignment and reservation state in its own document so that a routing transition never
        // contends with a CRM edit of the same activity row. The work state itself is a Contact Center document;
        // the base feature composes Omnichannel Management so its CRM activity projection and administration
        // surfaces are available together.
        services
            .AddScoped<IContactCenterWorkStateStore, ContactCenterWorkStateStore>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterWorkStateRetentionPolicy>()
            .AddScoped<IContactCenterWorkStateManager, ContactCenterWorkStateManager>()
            .AddScoped<IContactCenterWorkStateService, ContactCenterWorkStateService>()
            .AddIndexProvider<ContactCenterWorkStateIndexProvider>()
            .AddDataMigration<ContactCenterWorkStateIndexMigrations>();

        // The call-session index and its migration canonicalize provider identity, and this feature does not
        // depend on Telephony, so the resolver must also be available without the Telephony module.
        services.TryAddSingleton<IProviderIdentityResolver, ProviderIdentityResolver>();

        services
            .AddIndexProvider<CallSessionIndexProvider>()
            .AddDataMigration<CallSessionIndexMigrations>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, OutboxDispatchBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ContactCenterRetentionBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ContactCenterMetricRollupBackgroundTask>());
        services.AddPermissionProvider<ContactCenterPermissionProvider>();
    }
}
