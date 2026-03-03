using System.Threading.Channels;
using CrestApps.AI;
using CrestApps.AI.Chat.Hubs;
using CrestApps.AI.Chat.Models;
using CrestApps.AI.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.Mvc.Web.Hubs;

[Authorize]
public sealed class AIChatHub : AIChatHubBase
{
    public AIChatHub(
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IOrchestratorResolver orchestratorResolver,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        ILogger<AIChatHub> logger)
        : base(profileManager, sessionManager, promptStore, orchestratorResolver, orchestrationContextBuilder, logger)
    {
    }

    /// <summary>
    /// Overload matching the signature expected by the shared ai-chat.js client script.
    /// Creates a new session on the fly when <paramref name="sessionId"/> is empty.
    /// </summary>
    public ChannelReader<CompletionPartialMessage> SendMessage(
        string profileId,
        string prompt,
        string sessionId,
        string sessionProfileId,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = HandleSendMessageAsync(channel.Writer, profileId, prompt, sessionId);

        return channel.Reader;
    }

    private async Task HandleSendMessageAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        string profileId,
        string prompt,
        string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                await Clients.Caller.ReceiveError("Profile ID is required to start a new session.");
                writer.TryComplete();
                return;
            }

            var profile = await ProfileManager.FindByIdAsync(profileId);

            if (profile == null)
            {
                await Clients.Caller.ReceiveError("AI profile not found.");
                writer.TryComplete();
                return;
            }

            var session = await SessionManager.NewAsync(profile, new NewAIChatSessionContext());
            session.Title = profile.DisplayText ?? profile.Name;
            session.UserId = Context.User?.Identity?.Name ?? "anonymous";
            await SessionManager.SaveAsync(session);
            sessionId = session.SessionId;
        }

        await ProcessSendMessageAsync(writer, sessionId, prompt, []);
    }
}
