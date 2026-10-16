using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Providers;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Prompting.Drivers;
using CrestApps.OrchardCore.AI.Prompting.Providers;
using CrestApps.OrchardCore.AI.Prompting.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Prompting;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITemplating();

        // Replace default service with feature-aware version.
        services.Replace(ServiceDescriptor.Scoped<ITemplateService, OrchardCoreTemplateService>());

        // Add module directory scanner.
        services.AddSingleton<ITemplateProvider, ModuleTemplateProvider>();

        // System Prompt AI templates are read per scope by OrchardCoreTemplateService, not cached
        // with the other providers, so they are not registered as an ITemplateProvider.
        services.AddScoped<AIProfileSystemPromptTemplateProvider>();

        // Add display drivers for prompt selection UI.
        services.AddDisplayDriver<AIProfile, AIProfilePromptSelectionDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionPromptSelectionDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplatePromptSelectionDisplayDriver>();
    }
}
