using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.DeepSeek.Core.Services;
using CrestApps.OrchardCore.DeepSeek.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DeepSeek;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIProfile<DeepSeekProfileSource, DeepSeekAICompletionClient>(DeepSeekProfileSource.ImplementationName);

        services
            .AddAIDeploymentProvider<DeepSeekAIDeploymentProvider>(DeepSeekProfileSource.ProviderTechnicalName)
            .AddDataMigration<DefaultDeepSeekDeploymentMigrations>();
    }
}
