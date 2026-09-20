using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Workflows.Helpers;
using CrestApps.OrchardCore.ContactCenter.Core;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Work Distribution feature: queues, queue items, reservations, business hours,
/// availability-based assignment, and the policy-based routing strategies that distribute work to available
/// agents, together with their administration screens.
/// </summary>
[Feature(ContactCenterFeatures.Queues)]
public sealed class QueuesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterCapability(ContactCenterFeatures.Queues, ContactCenterCapabilities.Queues);

        services.AddCoreHostSeams();

        services.AddCoreContactCenterQueues();

        services
            .AddContactCenterOperationAuthorization()
            .AddScoped<ContactCenterAdminFormOptionsProvider>()
            .AddScoped<IHandoffQueueOptionsProvider, ContactCenterHandoffQueueOptionsProvider>();

        // The host's scheduler for the queue-treatment cycle.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, QueueTreatmentBackgroundTask>());

        // How the shared configuration cache learns a tenant's configuration changed. The Business Hours
        // feature also registers it; TryAdd keeps a single instance whichever feature runs first.
        services.TryAddSingleton<IContactCenterConfigurationChangeNotifier, SignalContactCenterConfigurationChangeNotifier>();

        services
            .AddScoped<ICatalogEntryHandler<ActivityQueueGroup>, ActivityQueueGroupHandler>()
            .AddScoped<ICatalogEntryHandler<ActivityQueue>, ActivityQueueHandler>()
            .AddScoped<ICatalogEntryHandler<ActivityQueue>, ContactCenterConfigurationCacheInvalidationHandler<ActivityQueue>>()
            .AddScoped<ICatalogEntryHandler<ContactCenterSkill>, ContactCenterSkillHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterSkill>, ContactCenterConfigurationCacheInvalidationHandler<ContactCenterSkill>>()
            .AddIndexProvider<ActivityQueueGroupIndexProvider>()
            .AddDataMigration<ActivityQueueGroupIndexMigrations>()
            .AddIndexProvider<ActivityQueueIndexProvider>()
            .AddDataMigration<ActivityQueueIndexMigrations>()
            .AddIndexProvider<ContactCenterSkillIndexProvider>()
            .AddDataMigration<ContactCenterSkillIndexMigrations>()
            .AddIndexProvider<QueueItemIndexProvider>()
            .AddDataMigration<QueueItemIndexMigrations>()
            .AddIndexProvider<ActivityReservationIndexProvider>()
            .AddDataMigration<ActivityReservationIndexMigrations>();

        // Queue and skill administration screens. (Business-hours calendars are administered by the Business Hours
        // feature this feature depends on.)
        services
            .AddDisplayDriver<ActivityQueueGroup, ActivityQueueGroupDisplayDriver>()
            .AddDisplayDriver<ActivityQueue, ActivityQueueDisplayDriver>()
            .AddDisplayDriver<ContactCenterSkill, ContactCenterSkillDisplayDriver>();

        services.AddNavigationProvider<ContactCenterAdminMenu>();

        services.AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
            new ContactCenterFeatureWorkLifecycleParticipant(
                ContactCenterCapabilities.Queues,
                serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        // The host's scheduler for the cycles AddCoreContactCenterQueues registered.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ReservationExpiryBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, DirectRingTimeoutBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, OrphanedActivityRecoveryBackgroundTask>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapContactCenterQueueSearchEndpoints();
    }
}

/// <summary>
/// Registers the deployment steps that export the routing configuration owned by the queues feature.
/// </summary>
[Feature(ContactCenterFeatures.Queues)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class QueuesDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterSkillDeploymentSource, ContactCenterSkillDeploymentStep>();
        services.AddDeployment<ContactCenterQueueGroupDeploymentSource, ContactCenterQueueGroupDeploymentStep>();
        services.AddDeployment<ContactCenterQueueDeploymentSource, ContactCenterQueueDeploymentStep>();
    }
}

/// <summary>
/// Registers the recipe steps that import the routing configuration owned by the queues feature.
/// </summary>
[Feature(ContactCenterFeatures.Queues)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class QueuesRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterSkillStep>();
        services.AddRecipeExecutionStep<ContactCenterQueueGroupStep>();
        services.AddRecipeExecutionStep<ContactCenterQueueStep>();
    }
}

/// <summary>
/// Registers the Enqueue Activity workflow task, available only when both Orchard Core Workflows and the
/// Queues feature are enabled so the required queue service is always resolvable.
/// </summary>
[Feature(ContactCenterFeatures.Queues)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterQueuesWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<EnqueueActivityTask, EnqueueActivityTaskDisplayDriver>();

        // Transferring to a human always lands in a queue — even the phone handoff seats the live call in one
        // before it is offered — so this belongs to the Queues feature rather than to either channel's module.
        // The per-channel handoff implementations are resolved as a set and may legitimately be empty.
        services.AddActivity<TransferToAgentTask, TransferToAgentTaskDisplayDriver>();
    }
}

/// <summary>
/// Registers the health checks owned by the Contact Center Queues feature, but only when the
/// <c>OrchardCore.HealthChecks</c> feature is also enabled so a deployment that does not use health checks never
/// pays for them.
/// </summary>
[Feature(ContactCenterFeatures.Queues)]
[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class ContactCenterQueuesHealthChecksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterQueuesHealthChecks();
    }
}
