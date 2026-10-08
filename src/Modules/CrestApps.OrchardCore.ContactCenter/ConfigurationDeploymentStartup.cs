using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the deployment steps that export the configuration the Contact Center base feature owns: the voice media
/// library and the approved external transfer destinations.
/// </summary>
/// <remarks>
/// Settings are exported through the standard site settings step, so they are imported by the built-in
/// <c>Settings</c> recipe step and need no import code of their own.
/// </remarks>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ContactCenterConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterVoiceMediaDeploymentSource, ContactCenterVoiceMediaDeploymentStep>();
        services.AddSiteSettingsPropertyDeploymentStep<ContactCenterExternalTransferSettings, ContactCenterConfigurationDeploymentStartup>(
            S => S["Contact Center External Transfer Settings"],
            S => S["Exports the approved external transfer destinations and whether agents may dial unlisted numbers."]);
    }
}

/// <summary>
/// Registers the recipe step that imports the voice media library.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ContactCenterConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterVoiceMediaStep>();
    }
}

/// <summary>
/// Registers the deployment step that exports the call recording and monitoring settings.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class RecordingDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ContactCenterRecordingSettings, RecordingDeploymentStartup>(
            S => S["Contact Center Recording Settings"],
            S => S["Exports the call recording, consent, retention and secure pause settings."]);
    }
}

/// <summary>
/// Registers the deployment step that exports the secure payment capture settings.
/// </summary>
[Feature(ContactCenterConstants.Feature.SecureCapture)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class SecureCaptureDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<SecureCaptureSettings, SecureCaptureDeploymentStartup>(
            S => S["Contact Center Secure Capture Settings"],
            S => S["Exports the secure capture settings."]);
    }
}
