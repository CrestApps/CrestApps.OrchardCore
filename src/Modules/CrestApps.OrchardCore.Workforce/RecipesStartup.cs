using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.Deployments;
using CrestApps.OrchardCore.Workforce.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Imports the Workforce recipe step.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<WorkforceRecipeStep>();
    }
}

/// <summary>
/// Exports the workforce setup with a deployment step.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<WorkforceDeploymentSource, WorkforceDeploymentStep, WorkforceDeploymentStepDisplayDriver>();
        services.AddDeployment<WorkforceSettingsDeploymentSource, WorkforceSettingsDeploymentStep, WorkforceSettingsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Workforce recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, WorkforceRecipeStepSchema>();
        services.AddDeploymentStepSchema<WorkforceDeploymentStepSchema>();
        services.AddDeploymentStepSchema<WorkforceSettingsDeploymentStepSchema>();
    }
}

/// <summary>
/// Imports the ShiftPlanning settings with its recipe step.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ShiftPlanningRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ShiftPlanningRecipeStep>();
    }
}

/// <summary>
/// Describes the ShiftPlanning recipe step to CrestApps Recipes.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class ShiftPlanningRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ShiftPlanningRecipeStepSchema>();
    }
}

/// <summary>
/// Imports the TimeOff settings with its recipe step.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class TimeOffRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TimeOffRecipeStep>();
    }
}

/// <summary>
/// Describes the TimeOff recipe step to CrestApps Recipes.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class TimeOffRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TimeOffRecipeStepSchema>();
    }
}

/// <summary>
/// Imports the TimeClock settings with its recipe step.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class TimeClockRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TimeClockRecipeStep>();
    }
}

/// <summary>
/// Describes the TimeClock recipe step to CrestApps Recipes.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class TimeClockRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TimeClockRecipeStepSchema>();
    }
}
