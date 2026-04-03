using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Prompting.Drivers;
using CrestApps.OrchardCore.AI.Prompting.Providers;
using CrestApps.OrchardCore.AI.Prompting.Services;
using CrestApps.Templates.Providers;
using CrestApps.Templates.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Prompting;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITemplating();

        // Replace default service with feature-aware version.
        services.Replace(ServiceDescriptor.Scoped<ITemplateService, OrchardCoreTemplateService>());

        // Add module directory scanner.
        services.AddSingleton<ITemplateProvider, ModuleTemplateProvider>();

        // Add display drivers for prompt selection UI.
        services.AddDisplayDriver<AIProfile, AIProfilePromptSelectionDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionPromptSelectionDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplatePromptSelectionDisplayDriver>();
    }
}
