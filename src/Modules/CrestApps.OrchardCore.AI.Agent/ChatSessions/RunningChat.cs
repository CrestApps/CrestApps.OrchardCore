using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;

namespace CrestApps.OrchardCore.AI.Agent.ChatSessions;

/// <summary>
/// The chat the current AI turn runs in, so a tool does not delete the conversation or interaction it is answering.
/// </summary>
internal static class RunningChat
{
    /// <summary>
    /// Gets the ID of the chat session the current AI turn runs in.
    /// </summary>
    /// <returns>The session ID, or <see langword="null"/> outside a chat session.</returns>
    public static string GetSessionId()
        => AIInvocationScope.Current?.ChatSession?.SessionId;

    /// <summary>
    /// Gets the user ID of the signed-in caller.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The user ID, or <see langword="null"/> when nobody is signed in.</returns>
    public static string GetCallerId(IServiceProvider services)
        => AgentToolAuthorization.GetCaller(services)?.FindFirstValue(ClaimTypes.NameIdentifier);
}
