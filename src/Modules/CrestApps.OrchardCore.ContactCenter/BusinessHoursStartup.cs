using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Business Hours feature: business-hours calendars and their administration. The calendars are a
/// source of the shared business hours (<see cref="BusinessHoursCalendarScheduleSource"/>), so every module that asks
/// the open-hours service, the Omnichannel business-hours gate and work-order service levels included, reads them
/// without depending on the Contact Center. It is a standalone feature so that work distribution, the outbound
/// dialer, and automated Omnichannel conversations can each depend on it without depending on one another.
/// </summary>
[Feature(ContactCenterConstants.Feature.BusinessHours)]
public sealed class BusinessHoursStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Calendars live in the Contact Center storage collection. Register it here so the feature is self-contained
        // when it is the only Contact Center feature enabled, as when Omnichannel Activities pulls it in on its own (the
        // base Area feature registers the same collection; the registration is idempotent).
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterStorage.CollectionName));

        services
            .AddScoped<IBusinessHoursCalendarStore, BusinessHoursCalendarStore>()
            .AddScoped<IBusinessHoursCalendarManager, BusinessHoursCalendarManager>()
            .AddScoped<IBusinessHoursService, DefaultBusinessHoursService>();

        // The calendars are read through the shared open-hours service, which the Omnichannel business-hours gate and
        // work-order service levels read too.
        services
            .AddOpenHours()
            .AddHoursScheduleSource<BusinessHoursCalendarScheduleSource>();

        // Omnichannel Activities registers the same gate; this feature registers it too, so the voice handoff and the
        // dialer find it when this feature is enabled without the activities.
        services.TryAddScoped<IBusinessHoursGate, OpenHoursBusinessHoursGate>();

        // The calendar manager caches configuration through the shared Contact Center cache, and the invalidation
        // handler keeps it fresh. Both this feature and Work Distribution register the cache, so TryAdd keeps a single
        // instance regardless of which feature is enabled or in what order they configure services.
        services.TryAddSingleton<IContactCenterConfigurationCache, ContactCenterConfigurationCache>();

        services
            .AddScoped<ICatalogEntryHandler<BusinessHoursCalendar>, BusinessHoursCalendarHandler>()
            .AddScoped<ICatalogEntryHandler<BusinessHoursCalendar>, ContactCenterConfigurationCacheInvalidationHandler<BusinessHoursCalendar>>()
            .AddIndexProvider<BusinessHoursCalendarIndexProvider>()
            .AddDataMigration<BusinessHoursCalendarIndexMigrations>()
            .AddDisplayDriver<BusinessHoursCalendar, BusinessHoursCalendarDisplayDriver>();

        services.AddNavigationProvider<BusinessHoursAdminMenu>();
        services.AddScoped<IPermissionProvider, BusinessHoursPermissionProvider>();
    }
}

/// <summary>
/// Registers the deployment step that exports business-hours calendars.
/// </summary>
[Feature(ContactCenterConstants.Feature.BusinessHours)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class BusinessHoursDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterBusinessHoursCalendarDeploymentSource, ContactCenterBusinessHoursCalendarDeploymentStep>();
    }
}

/// <summary>
/// Registers the recipe step that imports business-hours calendars.
/// </summary>
[Feature(ContactCenterConstants.Feature.BusinessHours)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class BusinessHoursRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterBusinessHoursCalendarStep>();
    }
}

