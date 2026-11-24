using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Users.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Rules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// Registers the audiences administrators define.
/// </summary>
[Feature(UsersConstants.Feature.Audiences)]
public sealed class AudiencesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddUserAudiences();
        services.AddPermissionProvider<UserAudiencePermissionProvider>();
        services.AddNavigationProvider<UserAudiencesAdminMenu>();
        services.AddDisplayDriver<UserAudienceRow, UserAudienceRowDisplayDriver>();
        services.AddDisplayDriver<UserAudienceMemberRow, UserAudienceMemberRowDisplayDriver>();
    }
}

/// <summary>
/// Registers the audiences recipe step.
/// </summary>
[Feature(UsersConstants.Feature.Audiences)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class AudiencesRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<UserAudiencesRecipeStep>();
    }
}

/// <summary>
/// Registers the audiences deployment step.
/// </summary>
[Feature(UsersConstants.Feature.Audiences)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class AudiencesDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<UserAudiencesDeploymentSource, UserAudiencesDeploymentStep, UserAudiencesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the audiences recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(UsersConstants.Feature.Audiences)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AudiencesRecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, UserAudiencesRecipeStepSchema>();
        services.AddDeploymentStepSchema<UserAudiencesDeploymentStepSchema>();
    }
}

/// <summary>
/// Registers the "In a user audience" rule condition for layers.
/// </summary>
[Feature(UsersConstants.Feature.Audiences)]
[RequireFeatures(OrchardCoreFeatureIds.Rules)]
public sealed class AudiencesRulesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRule<UserAudienceCondition, UserAudienceConditionEvaluator, UserAudienceConditionDisplayDriver>();
    }
}
