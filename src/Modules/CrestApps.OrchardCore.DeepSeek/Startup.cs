using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.DeepSeek.Migrations;
using CrestApps.OrchardCore.DeepSeek.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DeepSeek;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIProfile<DeepSeekAICompletionClient>(DeepSeekConstants.ImplementationName, DeepSeekConstants.ProviderTechnicalName, o =>
            {
                o.DisplayName = "DeepSeek";
                o.Description = "Provides AI profiles using DeepSeek.";
            });

        services
            .AddAIDeploymentProvider(DeepSeekConstants.ProviderTechnicalName, o =>
            {
                o.DisplayName = "DeepSeek";
                o.Description = "DeepSeek AI deployments.";
            }).AddDataMigration<DefaultDeepSeekDeploymentMigrations>();
    }
}
