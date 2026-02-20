using CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;
using CrestApps.OrchardCore.AI.Chat.Copilot.Handlers;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
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
        services
            .AddOrchestrator<CopilotOrchestrator>(CopilotOrchestrator.OrchestratorName)
            .WithTitle(S["GitHub Copilot Orchestrator"]);

        // Register HTTP client for GitHub API calls
        services.AddHttpClient()
            .AddScoped<GitHubOAuthService>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextHandler, CopilotOrchestrationContextHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChatInteractionSettingsHandler, CopilotChatInteractionSettingsHandler>());

        services.AddDisplayDriver<AIProfile, AIProfileCopilotDisplayDriver>();

        services.AddDisplayDriver<ChatInteraction, ChatInteractionCopilotDisplayDriver>();

        services.AddSiteDisplayDriver<CopilotSettingsDisplayDriver>();

        services.AddPermissionProvider<CopilotPermissionProvider>();
    }
}
