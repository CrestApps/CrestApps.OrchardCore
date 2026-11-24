using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.ChatSessions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that deletes an AI conversation, when AI chat is on. It deletes the caller's own conversations,
/// as a profile's chat History page does, and never the conversation the assistant is answering in.
/// </summary>
[RequireFeatures(AIConstants.Feature.Chat)]
public sealed class AIChatSessionsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionsToolsStartup(IStringLocalizer<AIChatSessionsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<DeleteAIConversationTool>(DeleteAIConversationTool.TheName)
            .WithTitle(S["Delete AI Conversation"])
            .WithDescription(S["Deletes one AI conversation and its transcript by session id. It cannot be undone. Only your own conversations can be deleted, as on a profile's chat History page."])
            .WithCategory(S["AI Conversations"])
            .RequireApproval()
            .Selectable();
    }
}
