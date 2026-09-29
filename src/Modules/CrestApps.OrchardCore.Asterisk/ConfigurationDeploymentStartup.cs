using CrestApps.OrchardCore.Asterisk.Deployments.Drivers;
using CrestApps.OrchardCore.Asterisk.Deployments.Sources;
using CrestApps.OrchardCore.Asterisk.Deployments.Steps;
using CrestApps.OrchardCore.Asterisk.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Asterisk;

/// <summary>
/// Registers the deployment step that exports the Asterisk provider settings.
/// </summary>
/// <remarks>
/// The settings get a step of their own rather than the standard site settings step because they hold data-protected
/// secrets: the standard step would copy the encrypted values, which no other environment can decrypt, and its import
/// replaces the stored settings whole, which would erase the destination's credentials.
/// </remarks>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AsteriskSettingsDeploymentSource, AsteriskSettingsDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, AsteriskSettingsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the recipe step that imports the Asterisk provider settings.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AsteriskSettingsStep>();
    }
}
