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

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Work Distribution feature: queues, queue items, reservations, business hours,
/// availability-based assignment, and the policy-based routing strategies that distribute work to available
/// agents, together with their administration screens.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class QueuesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IActivityQueueGroupStore, ActivityQueueGroupStore>()
            .AddScoped<IActivityQueueGroupManager, ActivityQueueGroupManager>()
            .AddScoped<IActivityQueueStore, ActivityQueueStore>()
            .AddScoped<IActivityQueueManager, ActivityQueueManager>()
            .AddScoped<ISupervisorQueueAuthorizationService, SupervisorQueueAuthorizationService>()
            .AddScoped<IContactCenterSkillStore, ContactCenterSkillStore>()
            .AddScoped<IContactCenterSkillManager, ContactCenterSkillManager>()
            .AddScoped<IBusinessHoursCalendarStore, BusinessHoursCalendarStore>()
            .AddScoped<IBusinessHoursCalendarManager, BusinessHoursCalendarManager>()
            .AddScoped<IBusinessHoursService, DefaultBusinessHoursService>()
            .AddScoped<IQueueItemStore, QueueItemStore>()
            .AddScoped<IQueueItemManager, QueueItemManager>()
            .AddScoped<IActivityReservationStore, ActivityReservationStore>()
            .AddScoped<IActivityReservationManager, ActivityReservationManager>()
            .AddScoped<IAgentWorkStateHealingService, AgentWorkStateHealingService>()
            .AddScoped<IActivityQueueService, ActivityQueueService>()
            .AddScoped<ActivityReservationService>()
            .AddScoped<IActivityReservationService>(static sp => sp.GetRequiredService<ActivityReservationService>())
            .AddScoped<IActivityReservationReclaimer>(static sp => sp.GetRequiredService<ActivityReservationService>())
            .AddScoped<IContactCenterRetentionPolicy, QueueItemRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ActivityReservationRetentionPolicy>()
            .AddScoped<ContactCenterAdminFormOptionsProvider>();

        services
            .AddSingleton<IContactCenterConfigurationCache, ContactCenterConfigurationCache>()
            .AddScoped<ICatalogEntryHandler<ActivityQueueGroup>, ActivityQueueGroupHandler>()
            .AddScoped<ICatalogEntryHandler<ActivityQueue>, ActivityQueueHandler>()
            .AddScoped<ICatalogEntryHandler<ActivityQueue>, ContactCenterConfigurationCacheInvalidationHandler<ActivityQueue>>()
            .AddScoped<ICatalogEntryHandler<ContactCenterSkill>, ContactCenterSkillHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterSkill>, ContactCenterConfigurationCacheInvalidationHandler<ContactCenterSkill>>()
            .AddScoped<ICatalogEntryHandler<BusinessHoursCalendar>, BusinessHoursCalendarHandler>()
            .AddScoped<ICatalogEntryHandler<BusinessHoursCalendar>, ContactCenterConfigurationCacheInvalidationHandler<BusinessHoursCalendar>>()
            .AddIndexProvider<ActivityQueueGroupIndexProvider>()
            .AddDataMigration<ActivityQueueGroupIndexMigrations>()
            .AddIndexProvider<ActivityQueueIndexProvider>()
            .AddDataMigration<ActivityQueueIndexMigrations>()
            .AddIndexProvider<ContactCenterSkillIndexProvider>()
            .AddDataMigration<ContactCenterSkillIndexMigrations>()
            .AddIndexProvider<BusinessHoursCalendarIndexProvider>()
            .AddDataMigration<BusinessHoursCalendarIndexMigrations>()
            .AddIndexProvider<QueueItemIndexProvider>()
            .AddDataMigration<QueueItemIndexMigrations>()
            .AddIndexProvider<ActivityReservationIndexProvider>()
            .AddDataMigration<ActivityReservationIndexMigrations>();

        // Queue, skill, business-hours, and agent-entitlement administration screens.
        services
            .AddDisplayDriver<ActivityQueueGroup, ActivityQueueGroupDisplayDriver>()
            .AddDisplayDriver<ActivityQueue, ActivityQueueDisplayDriver>()
            .AddDisplayDriver<ContactCenterSkill, ContactCenterSkillDisplayDriver>()
            .AddDisplayDriver<BusinessHoursCalendar, BusinessHoursCalendarDisplayDriver>();

        services.AddNavigationProvider<ContactCenterAdminMenu>();
        services.AddNavigationProvider<ContactCenterAgentEntitlementsAdminMenu>();

        // Policy-based routing strategies and activity assignment orchestration.
        services
            .AddScoped<IActivityRoutingService, ActivityRoutingService>()
            .AddScoped<IActivityRoutingStrategy, RequiredSkillsRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, CapacityRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, StickyAgentRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, LongestIdleRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, RoundRobinRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, LeastBusyRoutingStrategy>()
            .AddScoped<IActivityAssignmentService, ActivityAssignmentService>();

        services.AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
            new ContactCenterFeatureWorkLifecycleParticipant(
                ContactCenterConstants.Feature.Queues,
                serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ReservationExpiryBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, DirectRingTimeoutBackgroundTask>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddQueueSearchEndpoint();
    }
}

/// <summary>
/// Registers the deployment steps that export the routing configuration owned by the queues feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class QueuesDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterSkillDeploymentSource, ContactCenterSkillDeploymentStep>();
        services.AddDeployment<ContactCenterAgentEntitlementDeploymentSource, ContactCenterAgentEntitlementDeploymentStep>();
        services.AddDeployment<ContactCenterQueueGroupDeploymentSource, ContactCenterQueueGroupDeploymentStep>();
        services.AddDeployment<ContactCenterBusinessHoursCalendarDeploymentSource, ContactCenterBusinessHoursCalendarDeploymentStep>();
        services.AddDeployment<ContactCenterQueueDeploymentSource, ContactCenterQueueDeploymentStep>();
    }
}

/// <summary>
/// Registers the recipe steps that import the routing configuration owned by the queues feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class QueuesRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterSkillStep>();
        services.AddRecipeExecutionStep<ContactCenterAgentEntitlementStep>();
        services.AddRecipeExecutionStep<ContactCenterQueueGroupStep>();
        services.AddRecipeExecutionStep<ContactCenterBusinessHoursCalendarStep>();
        services.AddRecipeExecutionStep<ContactCenterQueueStep>();
    }
}

/// <summary>
/// Registers the Enqueue Activity workflow task, available only when both Orchard Core Workflows and the
/// Queues feature are enabled so the required queue service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterQueuesWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<EnqueueActivityTask, EnqueueActivityTaskDisplayDriver>();
    }
}

/// <summary>
/// Registers the health checks owned by the Contact Center Queues feature, but only when the
/// <c>OrchardCore.HealthChecks</c> feature is also enabled so a deployment that does not use health checks never
/// pays for them.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class ContactCenterQueuesHealthChecksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterQueuesHealthChecks();
    }
}
