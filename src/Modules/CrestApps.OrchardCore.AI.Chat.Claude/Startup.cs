using CrestApps.Core.AI.Claude;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.OrchardCore.AI.Chat.Claude.Drivers;
using CrestApps.OrchardCore.AI.Chat.Claude.Services;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Claude;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIClaudeOrchestrator();
        services.ConfigureOptions<ClaudeOptionsConfiguration>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestratorAvailabilityProvider, ClaudeOrchestratorAvailabilityProvider>());

        services.AddDisplayDriver<AIProfile, AIProfileClaudeDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateClaudeDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionClaudeDisplayDriver>();

        services
            .AddSiteDisplayDriver<ClaudeSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.AddPermissionProvider<ClaudePermissionProvider>();
    }
}
