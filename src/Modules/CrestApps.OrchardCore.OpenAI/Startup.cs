using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.OpenAI.Services;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Handlers;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

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

[RequireFeatures(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IModelHandler<AIProviderConnection>, OpenAIProviderConnectionSettingsHandler>();
        services.AddTransient<IAIProviderConnectionHandler, OpenAIProviderConnectionHandler>();
        services.AddDisplayDriver<AIProviderConnection, OpenAIProviderConnectionDisplayDriver>();
        services.AddAIConnectionSource(OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = "OpenAI";
            o.Description = "Provides a way to Configure OpenAI-compatible connection for any provider.";
        });
    }
}
