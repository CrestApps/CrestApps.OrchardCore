using CrestApps.OrchardCore.Telephony.Deployments.Drivers;
using CrestApps.OrchardCore.Telephony.Deployments.Sources;
using CrestApps.OrchardCore.Telephony.Deployments.Steps;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Registers the deployment steps that export the phone system configuration: internal extensions and the telephony
/// settings.
/// </summary>
/// <remarks>
/// Settings are exported through the standard site settings step, so they are imported by the built-in
/// <c>Settings</c> recipe step and need no import code of their own. Call logs, live calls and the tokens users grant a
/// provider are the phone system's operational record, not its configuration, and are deliberately left out.
/// </remarks>
[Feature(TelephonyConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TelephonyExtensionDeploymentSource, TelephonyExtensionDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, TelephonyExtensionDeploymentStepDisplayDriver>();
        services.AddSiteSettingsPropertyDeploymentStep<TelephonySettings, ConfigurationDeploymentStartup>(
            S => S["Telephony Settings"],
            S => S["Exports the default telephony provider and the short codes agents may dial."]);
    }
}

/// <summary>
/// Registers the recipe step that imports internal extensions.
/// </summary>
[Feature(TelephonyConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TelephonyExtensionStep>();
    }
}

/// <summary>
/// Registers the deployment step that exports the soft phone widget settings.
/// </summary>
[Feature(TelephonyConstants.Feature.SoftPhone)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class SoftPhoneWidgetDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<SoftPhoneWidgetSettings, SoftPhoneWidgetDeploymentStartup>(
            S => S["Soft Phone Widget Settings"],
            S => S["Exports where the soft phone widget is shown and how it looks."]);
    }
}
