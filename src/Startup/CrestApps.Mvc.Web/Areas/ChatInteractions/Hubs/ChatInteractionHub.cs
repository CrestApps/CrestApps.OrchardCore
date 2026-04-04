using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Hubs;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.Mvc.Web.Areas.ChatInteractions.Hubs;

[Authorize]
public sealed class ChatInteractionHub : ChatInteractionHubBase
{
    private readonly YesSql.ISession _session;

    public ChatInteractionHub(
        ICatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IOrchestratorResolver orchestratorResolver,
        IEnumerable<IChatInteractionSettingsHandler> settingsHandlers,
        TimeProvider timeProvider,
        YesSql.ISession session,
        ILogger<ChatInteractionHub> logger)
        : base(interactionManager, promptStore, orchestrationContextBuilder, orchestratorResolver, settingsHandlers, timeProvider, logger)
    {
        _session = session;
    }

    protected override async Task CommitChangesAsync()
        => await _session.SaveChangesAsync();
}
