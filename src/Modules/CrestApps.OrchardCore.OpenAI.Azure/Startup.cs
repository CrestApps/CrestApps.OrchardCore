using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatProfileSource<AzureOpenAIProfileSource>(AzureOpenAIProfileSource.Key);
    }
}

[Feature("CrestApps.OrchardCore.OpenAI.Azure.Core")]
public sealed class CoreStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public CoreStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<AzureCognitiveAccountOptions>(_shellConfiguration.GetSection("CrestApps_OpenAI_Azure"));
        services.Configure<AzureArmOptions>(_shellConfiguration.GetSection("CrestApps_Azure_Arm"));

        services.AddScoped<AzureOpenAIDeploymentsService>();
        services.AddScoped<IDisplayDriver<AIChatProfile>, AzureAIChatProfileDisplayDriver>();
    }
}

[Feature("CrestApps.OrchardCore.OpenAI.Azure.SearchAI")]
public sealed class SearchAzureAIStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatProfileSource<AzureOpenAIAzureAISearchProfileSource>(AzureOpenAIAzureAISearchProfileSource.Key);
        services.AddScoped<IDisplayDriver<AIChatProfile>, AzureAIChatProfileSearchAIDisplayDriver>();
    }
}
