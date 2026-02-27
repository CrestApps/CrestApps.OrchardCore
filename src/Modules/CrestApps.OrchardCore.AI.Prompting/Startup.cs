using CrestApps.AI.Prompting.Extensions;
using CrestApps.AI.Prompting.Providers;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Prompting.Drivers;
using CrestApps.OrchardCore.AI.Prompting.Providers;
using CrestApps.OrchardCore.AI.Prompting.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Prompting;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIPrompting();

        // Replace default service with feature-aware version.
        services.Replace(ServiceDescriptor.Scoped<IAITemplateService, OrchardCoreAITemplateService>());

        // Add module directory scanner.
        services.AddSingleton<IAITemplateProvider, ModuleAITemplateProvider>();

        // Add display drivers for prompt selection UI.
        services.AddDisplayDriver<AIProfile, AIProfilePromptSelectionDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionPromptSelectionDisplayDriver>();
    }
}
