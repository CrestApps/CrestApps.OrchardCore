using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.Drivers;
using CrestApps.OrchardCore.OpenAI.Azure.Handlers;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIDeploymentProvider(AzureOpenAIConstants.ProviderName, o =>
            {
                o.DisplayName = S["Azure OpenAI"];
                o.Description = S["Azure OpenAI model deployments."];
            });
    }
}

[Feature(AzureOpenAIConstants.Feature.Standard)]
public sealed class StandardStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public StandardStartup(IStringLocalizer<StandardStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<AzureOpenAICompletionClient>(AzureOpenAIConstants.StandardImplementationName, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["Azure OpenAI"];
            o.Description = S["Provides AI profiles using Azure OpenAI models."];
        });
    }
}

[Feature(AzureOpenAIConstants.Feature.AISearch)]
public sealed class AISearchStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public AISearchStartup(IStringLocalizer<AISearchStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<AzureAISearchCompletionClient>(AzureOpenAIConstants.AISearchImplementationName, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["Azure OpenAI with Azure AI Search"];
            o.Description = S["Provides AI profiles using Azure OpenAI models with your data."];
        });
        services.AddDisplayDriver<AIProfile, AzureOpenAIProfileSearchAIDisplayDriver>();
        services.AddScoped<IModelHandler<AIProfile>, AzureAISearchAIProfileHandler>();
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
        services.AddScoped<IModelHandler<AIProviderConnection>, AzureOpenAIConnectionSettingsHandler>();
        services.AddTransient<IAIProviderConnectionHandler, AzureOpenAIConnectionHandler>();
        services.AddDisplayDriver<AIProviderConnection, AzureOpenAIConnectionDisplayDriver>();
        services.AddAIConnectionSource(AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["Azure OpenAI"];
            o.Description = S["Provides a way to configure Azure OpenAI connections."];
        });
    }
}
