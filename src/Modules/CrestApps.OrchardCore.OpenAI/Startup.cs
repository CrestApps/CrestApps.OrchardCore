using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.OpenAI.Services;
using CrestApps.OrchardCore.OpenAI.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatCompletionService<OpenAIChatCompletionService>(OpenAIProfileSource.Key);
        services.AddAIChatProfileSource<OpenAIProfileSource>(OpenAIProfileSource.Key);
        services.AddDataMigration<OpenAITitleGeneratorProfileMigrations>();
    }
}

