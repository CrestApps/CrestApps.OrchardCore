using CrestApps.AI;
using CrestApps.AI.Chat.Copilot.Handlers;
using CrestApps.AI.Chat.Copilot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.AI.Chat.Copilot;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Copilot orchestrator and related services.
    /// </summary>
    public static IServiceCollection AddCopilotOrchestrator(this IServiceCollection services)
    {
        services.AddHttpClient();

        services.AddOrchestrator<CopilotOrchestrator>(CopilotOrchestrator.OrchestratorName)
            .WithTitle("GitHub Copilot Orchestrator");

        services.TryAddScoped<GitHubOAuthService>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChatInteractionSettingsHandler, CopilotChatInteractionSettingsHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, CopilotOrchestrationContextHandler>());

        return services;
    }
}
