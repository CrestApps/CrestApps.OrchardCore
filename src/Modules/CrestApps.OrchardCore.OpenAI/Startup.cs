using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.AI.OpenAI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Handlers;
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
        services.AddScoped<IAIClientProvider, OpenAIClientProvider>();
        services.AddCoreAIProfile<OpenAICompletionClient>(OpenAIConstants.ImplementationName, OpenAIConstants.ClientName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["Provides AI profiles using OpenAI."];
        });

        services.AddCoreAIDeploymentProvider(OpenAIConstants.ClientName, o =>
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
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, OpenAIProviderConnectionSettingsHandler>();
        services.AddTransient<IAIProviderConnectionHandler, OpenAIProviderConnectionHandler>();
        services.AddDisplayDriver<AIProviderConnection, OpenAIProviderConnectionDisplayDriver>();
        services.AddCoreAIConnectionSource(OpenAIConstants.ClientName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["Provides a way to configure OpenAI-compatible connection for any provider."];
        });
    }
}
