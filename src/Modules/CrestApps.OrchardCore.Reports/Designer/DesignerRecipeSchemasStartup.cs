using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Reports.Designer.Deployments;
using CrestApps.OrchardCore.Reports.Designer.Recipes;
using CrestApps.OrchardCore.Reports.Designer.Recipes.Workflows;
using CrestApps.OrchardCore.Reports.Widgets.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Describes the designed reports recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class DesignerRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ReportDesignsRecipeStepSchema>();
        services.AddDeploymentStepSchema<ReportDesignsDeploymentStepSchema>();
    }
}

/// <summary>
/// Describes the Report Builder workflow activities to CrestApps Recipes.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class DesignerWorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<ReportRunEventSchema>()
            .AddWorkflowActivitySchema<ReportExportedEventSchema>()
            .AddWorkflowActivitySchema<ReportPublishedEventSchema>()
            .AddWorkflowActivitySchema<RunReportTaskSchema>()
            .AddWorkflowActivitySchema<ExportReportTaskSchema>()
            .AddWorkflowActivitySchema<CreateReportShareLinkTaskSchema>();
    }
}

/// <summary>
/// Describes the Email Report workflow task to CrestApps Recipes when emailing is on.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows, OrchardCoreFeatureIds.Email)]
public sealed class DesignerEmailWorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<EmailReportTaskSchema>();
    }
}

/// <summary>
/// Describes the part of the designed report dashboard widget to CrestApps Recipes when the admin dashboard is on.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class DesignWidgetRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, ReportDesignWidgetPartSchemaDefinition>();
    }
}
