using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Skills.Deployments;
using CrestApps.OrchardCore.Skills.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Skills;

/// <summary>
/// Imports the Skills recipe step.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<SkillsRecipeStep>();
    }
}

/// <summary>
/// Exports skills and certifications with a deployment step.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<SkillsDeploymentSource, SkillsDeploymentStep, SkillsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Skills recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, SkillsRecipeStepSchema>();
        services.AddDeploymentStepSchema<SkillsDeploymentStepSchema>();
    }
}
