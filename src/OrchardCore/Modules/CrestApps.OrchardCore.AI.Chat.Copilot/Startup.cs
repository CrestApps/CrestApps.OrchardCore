using CrestApps.AI.Copilot;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Copilot;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register framework-level Copilot services (orchestrator, OAuth, handlers).
        services.AddCopilotOrchestrator();

        // Bridge OrchardCore site settings → CopilotOptions.
        services.ConfigureOptions<CopilotOptionsConfiguration>();

        // Bridge OrchardCore User model → ICopilotCredentialStore.
        services.AddScoped<ICopilotCredentialStore, OrchardCoreCopilotCredentialStore>();
        services.AddScoped<CopilotCallbackUrlProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestratorAvailabilityProvider, CopilotOrchestratorAvailabilityProvider>());

        // OrchardCore-specific display drivers.
        services.AddDisplayDriver<AIProfile, AIProfileCopilotDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionCopilotDisplayDriver>();

        services
            .AddSiteDisplayDriver<CopilotSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.AddPermissionProvider<CopilotPermissionProvider>();
    }
}
