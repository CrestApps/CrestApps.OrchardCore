using CrestApps.OrchardCore.Telnyx.Deployments.Drivers;
using CrestApps.OrchardCore.Telnyx.Deployments.Sources;
using CrestApps.OrchardCore.Telnyx.Deployments.Steps;
using CrestApps.OrchardCore.Telnyx.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the deployment step that exports the Telnyx voice provider settings.
/// </summary>
/// <remarks>
/// The settings get a step of their own rather than the standard site settings step because they hold data-protected
/// secrets: the standard step would copy the encrypted values, which no other environment can decrypt, and its import
/// replaces the stored settings whole, which would erase the destination's credentials.
/// </remarks>
[Feature(TelnyxConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TelnyxSettingsDeploymentSource, TelnyxSettingsDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, TelnyxSettingsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the recipe step that imports the Telnyx voice provider settings.
/// </summary>
[Feature(TelnyxConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TelnyxSettingsStep>();
    }
}

/// <summary>
/// Registers the deployment step that exports the Telnyx SMS provider settings.
/// </summary>
[Feature(TelnyxConstants.Feature.Sms)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class SmsConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TelnyxSmsSettingsDeploymentSource, TelnyxSmsSettingsDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, TelnyxSmsSettingsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the recipe step that imports the Telnyx SMS provider settings.
/// </summary>
[Feature(TelnyxConstants.Feature.Sms)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class SmsConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TelnyxSmsSettingsStep>();
    }
}
