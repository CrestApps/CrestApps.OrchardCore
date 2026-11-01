using CrestApps.Core.AI;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.ContactCenter.AI;
using CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;
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
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.ContactCenter.Workflows;
using CrestApps.OrchardCore.Workforce.Reports;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
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
            .AddIndexProvider<CapacityPlanIndexProvider>()
            .AddDataMigration<ForecastingMigrations>();

        services.TryAddScoped<PlanningGroupStore>();
        services.TryAddScoped<PlanningGroupManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>());
        services.TryAddScoped<ForecastEventStore>();
        services.TryAddScoped<ForecastEventManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ForecastEvent>, ForecastEventHandler>());
        services.TryAddScoped<ForecastService>();
        services.TryAddScoped<CapacityPlanStore>();
        services.TryAddScoped<CapacityPlanManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<CapacityPlan>, CapacityPlanHandler>());
        services.TryAddScoped<CapacityPlanService>();

        services.AddDisplayDriver<PlanningGroup, PlanningGroupRowDisplayDriver>();
        services.AddDisplayDriver<ForecastEvent, ForecastEventRowDisplayDriver>();
        services.AddDisplayDriver<ForecastRow, ForecastRowDisplayDriver>();
        services.AddDisplayDriver<CapacityPlanRow, CapacityPlanRowDisplayDriver>();
        services.AddNavigationProvider<ForecastingAdminMenu>();
    }
}

/// <summary>
/// Registers intraday management: the day as it happens, reforecasts and voluntary overtime and time off.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Intraday)]
public sealed class IntradayStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddIndexProvider<IntradayOfferIndexProvider>()
            .AddDataMigration<IntradayMigrations>();

        services.TryAddScoped<IntradayService>();
        services.AddScoped<IMyWorkLinkProvider, IntradayOffersLinkProvider>();
        services.AddNavigationProvider<IntradayAdminMenu>();
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

/// <summary>
/// Registers the forecast intervals data set of the Workforce report data source.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Forecasting)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class ForecastingReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkforceReportDataSet, ForecastIntervalsReportDataSet>();
    }
}

/// <summary>
/// Adds the forecast summary AI tool.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Forecasting)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class ForecastingAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastingAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ForecastingAIToolsStartup(IStringLocalizer<ForecastingAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ForecastSummaryTool>(ForecastSummaryTool.ToolName)
            .WithTitle(S["Forecast summary"])
            .WithDescription(S["Summarizes the active contact center forecast of planning groups for a day."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the out of adherence AI tool.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AdherenceAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceAIToolsStartup(IStringLocalizer<AdherenceAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<OutOfAdherenceTool>(OutOfAdherenceTool.ToolName)
            .WithTitle(S["Who's out of adherence"])
            .WithDescription(S["Lists the agents out of adherence now in the teams the user manages."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Imports the WorkforceForecasting recipe step.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Forecasting)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ForecastingRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ForecastingRecipeStep>();
    }
}

/// <summary>
/// Describes the WorkforceForecasting recipe step to CrestApps Recipes.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Forecasting)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class ForecastingRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ForecastingRecipeStepSchema>();
    }
}

/// <summary>
/// Imports the WorkforceAdherence recipe step.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class AdherenceRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AdherenceRecipeStep>();
    }
}

/// <summary>
/// Describes the WorkforceAdherence recipe step to CrestApps Recipes.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class AdherenceRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AdherenceRecipeStepSchema>();
    }
}
