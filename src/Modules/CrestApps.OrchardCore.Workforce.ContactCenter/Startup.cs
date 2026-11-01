using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;
using CrestApps.OrchardCore.Workforce.ContactCenter.Handlers;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Migrations;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Navigation;
using CrestApps.OrchardCore.Workforce.ContactCenter.Reports;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.ContactCenter.Workflows;
using CrestApps.OrchardCore.Workforce.Reports;
using CrestApps.OrchardCore.Workforce.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Workforce.ContactCenter;

/// <summary>
/// Registers the link between agents and workers.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<AgentWorkerService>();
        services.TryAddScoped(sp => new Lazy<AgentWorkerService>(sp.GetRequiredService<AgentWorkerService>));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<AgentProfile>, AgentWorkerHandler>());
        services.AddDisplayDriver<AgentWorkerRow, AgentWorkerRowDisplayDriver>();
        services.AddNavigationProvider<WorkforceContactCenterAdminMenu>();
    }
}

/// <summary>
/// Registers the sign-in punches.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.SignInPunches)]
public sealed class SignInPunchesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterEventHandler, SignInPunchHandler>();
    }
}

/// <summary>
/// Registers adherence: its records, the service, the pages and the background tasks.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
public sealed class AdherenceStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<AdherencePermissionsProvider>();
        services
            .AddIndexProvider<AdherenceRuleSetIndexProvider>()
            .AddIndexProvider<AdherenceExceptionIndexProvider>()
            .AddIndexProvider<AdherenceDayIndexProvider>()
            .AddIndexProvider<AdherenceAlertIndexProvider>()
            .AddDataMigration<AdherenceMigrations>();

        services.TryAddScoped<AdherenceRuleSetStore>();
        services.TryAddScoped<AdherenceRuleSetManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<AdherenceRuleSet>, AdherenceRuleSetHandler>());
        services.TryAddScoped<AdherenceService>();

        services.AddDisplayDriver<AdherenceRuleSet, AdherenceRuleSetRowDisplayDriver>();
        services.AddDisplayDriver<AdherenceExceptionRow, AdherenceExceptionRowDisplayDriver>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AdherenceAlertsBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AdherenceHistoryBackgroundTask>());
        services.AddNavigationProvider<AdherenceAdminMenu>();
    }
}

/// <summary>
/// Registers forecasting: planning groups, forecast events, forecasts and their pages.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Forecasting)]
public sealed class ForecastingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<ForecastingPermissionsProvider>();
        services
            .AddIndexProvider<PlanningGroupIndexProvider>()
            .AddIndexProvider<ForecastEventIndexProvider>()
            .AddIndexProvider<ForecastIndexProvider>()
            .AddDataMigration<ForecastingMigrations>();

        services.TryAddScoped<PlanningGroupStore>();
        services.TryAddScoped<PlanningGroupManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>());
        services.TryAddScoped<ForecastEventStore>();
        services.TryAddScoped<ForecastEventManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ForecastEvent>, ForecastEventHandler>());
        services.TryAddScoped<ForecastService>();

        services.AddDisplayDriver<PlanningGroup, PlanningGroupRowDisplayDriver>();
        services.AddDisplayDriver<ForecastEvent, ForecastEventRowDisplayDriver>();
        services.AddDisplayDriver<ForecastRow, ForecastRowDisplayDriver>();
        services.AddNavigationProvider<ForecastingAdminMenu>();
    }
}

/// <summary>
/// Registers the Agent Out Of Adherence workflow event.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class AdherenceWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<WorkforceOutOfAdherenceEvent, WorkforceEventActivityDisplayDriver<WorkforceOutOfAdherenceEvent>>();
    }
}

/// <summary>
/// Registers the adherence data sets of the Workforce report data source.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class AdherenceReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkforceReportDataSet, AdherenceDaysReportDataSet>();
        services.AddScoped<IWorkforceReportDataSet, AdherenceSegmentsReportDataSet>();
    }
}
