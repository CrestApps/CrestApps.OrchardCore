using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.OpenAI.Services;
using CrestApps.OrchardCore.DeepSeek.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<OpenAICompletionClient>(OpenAIConstants.ImplementationName, OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = "OpenAI";
            o.Description = "Provides AI profiles using OpenAI.";
        });

        services.AddAIDeploymentProvider(OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = "OpenAI";
            o.Description = "OpenAI deployments.";
        });
    }
}

[Feature(OpenAIConstants.Feature.Settings)]
public sealed class OpenAISettingsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<OpenAIConnectionsAdminMenu>();
        services.AddPermissionProvider<OpenAIPermissionProvider>();
        services.AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderOptionsConfiguration>();
        services.AddAIProfile<OpenAISettingsAICompletionClient>(OpenAIConstants.OpenAISettingsImplementationName, OpenAIConstants.OpenAISettingsProviderName, o =>
        {
            o.DisplayName = "Configured OpenAI";
            o.Description = "Provides AI profiles from any Configured OpenAI settings.";
        });

        services.AddAIDeploymentProvider(OpenAIConstants.OpenAISettingsProviderName, o =>
        {
            o.DisplayName = "Configured OpenAI";
            o.Description = "Provides AI deployment from any Configured OpenAI settings.";
        });
    }
}
