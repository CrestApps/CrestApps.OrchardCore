using CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddOrchestrator<CopilotOrchestrator>(CopilotOrchestrator.OrchestratorName)
            .WithTitle(S["GitHub Copilot Orchestrator"]);

        // Register HTTP client for GitHub API calls
        services.AddHttpClient();

        // Register GitHub OAuth service
        services.AddScoped<IGitHubOAuthService, GitHubOAuthService>();

        // Register display driver for Copilot-specific profile configuration
        services.AddDisplayDriver<AIProfile, AIProfileCopilotDisplayDriver>();

        // Register settings display driver
        services.AddSiteDisplayDriver<CopilotSettingsDisplayDriver>();

        // Register permissions
        services.AddPermissionProvider<Permissions>();
    }
}
