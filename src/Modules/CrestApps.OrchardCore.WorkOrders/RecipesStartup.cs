using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.WorkOrders.Recipes;
using CrestApps.OrchardCore.WorkOrders.Recipes.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// Imports the WorkOrders recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<WorkOrdersRecipeStep>();
    }
}

/// <summary>
/// Exports the WorkOrders deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<WorkOrdersDeploymentSource, WorkOrdersDeploymentStep, WorkOrdersDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the WorkOrders recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, WorkOrdersRecipeStepSchema>();
        services.AddDeploymentStepSchema<WorkOrdersDeploymentStepSchema>();
    }
}

/// <summary>
/// Describes the work order workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<WorkOrderCreatedEventSchema>()
            .AddWorkflowActivitySchema<WorkOrderStatusChangedEventSchema>()
            .AddWorkflowActivitySchema<WorkOrderSlaWarningEventSchema>()
            .AddWorkflowActivitySchema<WorkOrderSlaBreachedEventSchema>()
            .AddWorkflowActivitySchema<WorkOrderCompletedEventSchema>();
    }
}

/// <summary>
/// Describes the Service Agreement Expiring workflow event to CrestApps Recipes.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class AgreementsWorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<ServiceAgreementExpiringEventSchema>();
    }
}
