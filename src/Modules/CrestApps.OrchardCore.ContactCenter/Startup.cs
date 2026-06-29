using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the services and configuration for the base Contact Center feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterConstants.CollectionName));

        services
            .AddScoped<IInteractionStore, InteractionStore>()
            .AddScoped<IInteractionManager, InteractionManager>()
            .AddScoped<IInteractionEventStore, InteractionEventStore>()
            .AddScoped<IContactCenterEventPublisher, DefaultContactCenterEventPublisher>()
            .AddScoped<ICatalogEntryHandler<Interaction>, InteractionHandler>();

        services
            .AddIndexProvider<InteractionIndexProvider>()
            .AddDataMigration<InteractionIndexMigrations>();

        services
            .AddIndexProvider<InteractionEventIndexProvider>()
            .AddDataMigration<InteractionEventIndexMigrations>();

        services.AddPermissionProvider<ContactCenterPermissionProvider>();
        services.AddNavigationProvider<ContactCenterAdminMenu>();
    }
}

/// <summary>
/// Registers agent profiles, presence, capacity, skills, and queue/campaign sign-in.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class AgentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAgentProfileStore, AgentProfileStore>()
            .AddScoped<IAgentProfileManager, AgentProfileManager>()
            .AddScoped<IAgentPresenceManager, AgentPresenceManagerService>();

        services
            .AddIndexProvider<AgentProfileIndexProvider>()
            .AddDataMigration<AgentProfileIndexMigrations>();
    }
}

/// <summary>
/// Registers queues, queue items, reservations, and availability-based assignment.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class QueuesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IActivityQueueStore, ActivityQueueStore>()
            .AddScoped<IActivityQueueManager, ActivityQueueManager>()
            .AddScoped<IQueueItemStore, QueueItemStore>()
            .AddScoped<IQueueItemManager, QueueItemManager>()
            .AddScoped<IActivityReservationStore, ActivityReservationStore>()
            .AddScoped<IActivityReservationManager, ActivityReservationManager>()
            .AddScoped<IActivityQueueService, ActivityQueueService>()
            .AddScoped<IActivityReservationService, ActivityReservationService>()
            .AddScoped<IActivityAssignmentService, ActivityAssignmentService>();

        services
            .AddIndexProvider<ActivityQueueIndexProvider>()
            .AddDataMigration<ActivityQueueIndexMigrations>()
            .AddIndexProvider<QueueItemIndexProvider>()
            .AddDataMigration<QueueItemIndexMigrations>()
            .AddIndexProvider<ActivityReservationIndexProvider>()
            .AddDataMigration<ActivityReservationIndexMigrations>();

        services.AddSingleton<IBackgroundTask, ReservationExpiryBackgroundTask>();
    }
}

/// <summary>
/// Registers dialer-agnostic outbound dialing profiles, pacing, and dialer activity batch sources.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
public sealed class DialerStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public DialerStartup(IStringLocalizer<DialerStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IDialerProfileStore, DialerProfileStore>()
            .AddScoped<IDialerProfileManager, DialerProfileManager>()
            .AddScoped<IDialerService, DialerService>()
            .AddScoped<IDialerProviderResolver, DialerProviderResolver>();

        services
            .AddIndexProvider<DialerProfileIndexProvider>()
            .AddDataMigration<DialerProfileIndexMigrations>();

        services.AddSingleton<IBackgroundTask, DialerPacingBackgroundTask>();

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.PreviewDial, entry =>
            {
                entry.DisplayName = S["Preview dial batch"];
                entry.Description = S["Loads unassigned activities the dialer offers to agents one at a time for review before dialing."];
                entry.RequiresUserAssignment = false;
            });

            options.AddSource(ActivitySources.PowerDial, entry =>
            {
                entry.DisplayName = S["Power dial batch"];
                entry.Description = S["Loads unassigned activities the dialer dials automatically for available agents."];
                entry.RequiresUserAssignment = false;
            });
        });
    }
}
