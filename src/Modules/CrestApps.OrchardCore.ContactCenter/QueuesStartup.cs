using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers queues, queue items, reservations, and availability-based assignment.
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
            .AddScoped<IActivityReservationService, ActivityReservationService>()
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

        services.AddContactCenterQueuesHealthChecks();
    }
}
