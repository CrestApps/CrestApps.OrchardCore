using CrestApps.Core.Services;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Controllers;
using CrestApps.OrchardCore.Staff.Core;
using CrestApps.OrchardCore.Staff.Core.Indexes;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Deployments;
using CrestApps.OrchardCore.Staff.Drivers;
using CrestApps.OrchardCore.Staff.Endpoints;
using CrestApps.OrchardCore.Staff.Migrations;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Navigation;
using CrestApps.OrchardCore.Staff.Recipes;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Users.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Helpers;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// Registers the staff record, its admin screens, its editor, the Staff section of the user editor and the picker.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(StaffConstants.CollectionName));
        services.AddDataMigration<StaffMigrations>()
            .AddIndexProvider<StaffMemberIndexProvider>()
            .AddIndexProvider<StaffHistoryIndexProvider>();

        services.AddStaffCore();
        services.AddRoleGrants();
        services.AddDisplayDriver<StaffMember, StaffMemberDisplayDriver>();
        services.AddDisplayDriver<User, UserStaffDisplayDriver>();
        services.AddScoped<IUserEventHandler, StaffUserEventHandler>();
        services.AddPermissionProvider<StaffPermissionsProvider>();
        services.AddNavigationProvider<StaffAdminMenu>();
        services.AddPersonalDataContributor<StaffPersonalDataContributor>();
        services.AddSingleton<IBackgroundTask, StaffTerminationBackgroundTask>();
        services.AddSiteDisplayDriver<StaffSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(StaffSettingsDisplayDriver.GroupId, StaffPermissions.ManageStaff);
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddStaffSearchEndpoint();
    }
}

/// <summary>
/// Pay and cost rates on staff members.
/// </summary>
[Feature(StaffConstants.Features.Compensation)]
public sealed class CompensationStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<StaffMember, StaffPayDisplayDriver>();
    }
}

/// <summary>
/// Badge PINs on staff members.
/// </summary>
[Feature(StaffConstants.Features.Badges)]
public sealed class BadgesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<StaffMember, StaffBadgeDisplayDriver>();
    }
}

/// <summary>
/// The organization chart.
/// </summary>
[Feature(StaffConstants.Features.OrgChart)]
public sealed class OrgChartStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<StaffOrgChartAdminMenu>();
    }
}

/// <summary>
/// Makes a staff member and their user one skill holder, and adds the Skills card to the staff editor.
/// </summary>
[RequireFeatures(SkillsConstants.Features.Skills)]
public sealed class SkillsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffSkillLinks();
        services.AddDisplayDriver<StaffMember, StaffSkillsDisplayDriver>();
    }
}

/// <summary>
/// Makes staff members a party kind.
/// </summary>
public sealed class PartiesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPartySource, StaffPartySource>());
    }
}

/// <summary>
/// Finds the coordinates of staff home bases.
/// </summary>
[RequireFeatures(GeoConstants.Features.Geo)]
public sealed class GeoStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        // The queue's targets need the staff manager, whose handlers need the queue: the handler takes it lazily.
        services.TryAddScoped(sp => new Lazy<IGeocodeQueue>(sp.GetRequiredService<IGeocodeQueue>));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<StaffMember>, StaffHomeBaseGeocodeHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGeocodeTarget, StaffHomeBaseGeocodeTarget>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGeocodeTarget, LegacyWorkerHomeBaseGeocodeTarget>());
    }
}

/// <summary>
/// Adds the Staff report data source: staff members and their employment history.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<Reports.StaffReportDataSource>();
        services.AddScoped<Reports.IStaffReportDataSet, Reports.StaffMembersReportDataSet>();
        services.AddScoped<Reports.IStaffReportDataSet, Reports.StaffHistoryReportDataSet>();
    }
}

/// <summary>
/// Starts workflows when people start working, leave or get a new manager.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<Workflows.StaffMemberHiredEvent, Workflows.StaffEventActivityDisplayDriver<Workflows.StaffMemberHiredEvent>>();
        services.AddActivity<Workflows.StaffMemberLeftEvent, Workflows.StaffEventActivityDisplayDriver<Workflows.StaffMemberLeftEvent>>();
        services.AddActivity<Workflows.StaffManagerChangedEvent, Workflows.StaffEventActivityDisplayDriver<Workflows.StaffManagerChangedEvent>>();
        services.AddStaffLifecycleObserver<Workflows.StaffWorkflowLifecycleObserver>();
    }
}

/// <summary>
/// Describes the Staff workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<Workflows.StaffMemberHiredEventSchema>()
            .AddWorkflowActivitySchema<Workflows.StaffMemberLeftEventSchema>()
            .AddWorkflowActivitySchema<Workflows.StaffManagerChangedEventSchema>();
    }
}

/// <summary>
/// Imports the Staff recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<StaffRecipeStep>();
    }
}

/// <summary>
/// Exports staff with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<StaffDeploymentSource, StaffDeploymentStep, StaffDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Staff recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, StaffRecipeStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, StaffSettingsSchema>();
        services.AddDeploymentStepSchema<StaffDeploymentStepSchema>();
    }
}
