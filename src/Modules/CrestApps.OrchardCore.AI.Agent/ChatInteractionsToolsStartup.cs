using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.ChatInteractions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list and delete chat interactions, when AI Chat Interactions is on.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatInteractions)]
public sealed class ChatInteractionsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatInteractionsToolsStartup(IStringLocalizer<ChatInteractionsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListChatInteractionsTool>(ListChatInteractionsTool.TheName)
            .WithTitle(S["List Chat Interactions"])
            .WithDescription(S["Lists chat interactions with title, owner, deployment and last activity, with search and paging. Without the \"for others\" permission only the user's own are listed."])
            .WithCategory(S["AI Chat Interactions"])
            .Selectable();

        services.AddCoreAITool<DeleteChatInteractionsTool>(DeleteChatInteractionsTool.TheName)
            .WithTitle(S["Delete Chat Interactions"])
            .WithDescription(S["Deletes chat interactions by id, with their messages and uploaded documents. Interactions the user may not delete are skipped and reported."])
            .WithCategory(S["AI Chat Interactions"])
            .RequireApproval()
            .Selectable();
    }
}
