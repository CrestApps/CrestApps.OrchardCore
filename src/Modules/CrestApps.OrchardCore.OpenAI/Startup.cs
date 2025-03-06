using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.OpenAI.Services;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Handlers;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<OpenAICompletionClient>(OpenAIConstants.ImplementationName, OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["Provides AI profiles using OpenAI."];
        });

        services.AddAIDeploymentProvider(OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["OpenAI model deployments."];
        });
    }
}

[RequireFeatures(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ConnectionManagementStartup(IStringLocalizer<ConnectionManagementStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IModelHandler<AIProviderConnection>, OpenAIProviderConnectionSettingsHandler>();
        services.AddTransient<IAIProviderConnectionHandler, OpenAIProviderConnectionHandler>();
        services.AddDisplayDriver<AIProviderConnection, OpenAIProviderConnectionDisplayDriver>();
        services.AddAIConnectionSource(OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["Provides a way to configure OpenAI-compatible connection for any provider."];
        });
    }
}
