using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.DeepSeek.Core;
using CrestApps.OrchardCore.DeepSeek.Core.Services;
using CrestApps.OrchardCore.DeepSeek.Drivers;
using CrestApps.OrchardCore.DeepSeek.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DeepSeek;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeepSeekChatServices();
    }
}

[Feature(DeepSeekConstants.Feature.Chat)]
public sealed class ChatStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDisplayDriver<AIChatProfile, DeepSeekChatProfileDisplayDriver>();
    }
}

[Feature(DeepSeekConstants.Feature.Chat)]
[RequireFeatures(AIConstants.Feature.Deployments)]
public sealed class ChatDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIDeploymentProvider<DeepSeekAIDeploymentProvider>(DeepSeekConstants.DeepSeekProviderName);
    }
}

[Feature(DeepSeekConstants.Feature.CloudChat)]
public sealed class CloudChatStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetWeatherFunction>();

        services.AddAIChatCompletionService<DeepSeekCloudChatCompletionService>(DeepSeekCloudChatProfileSource.Key);

        services
            .AddDataMigration<DefaultDeepSeekDeploymentMigrations>();

        services
            .AddAIChatProfileSource<DeepSeekCloudChatProfileSource>(DeepSeekCloudChatProfileSource.Key)
            .AddDataMigration<DeepSeekTitleGeneratorProfileMigrations>();
    }
}
