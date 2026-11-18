using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Agent.ChatInteractions;

/// <summary>
/// What the chat interaction tools share.
/// </summary>
internal static class ChatInteractionToolSupport
{
    /// <summary>
    /// The key the Profile Builder marks its sessions with. The type lives in the chat interactions module, which this
    /// module does not reference, so the entry is read by its name.
    /// </summary>
    public const string ProfileBuilderMetadataKey = "ProfileBuilderInteractionMetadata";

    /// <summary>
    /// Whether an interaction is a Profile Builder session, which the Profile Builder lists and manages, not the Chat
    /// Interactions page.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns><see langword="true"/> for a Profile Builder session.</returns>
    public static bool IsProfileBuilderSession(ChatInteraction interaction)
        => interaction.Properties?.ContainsKey(ProfileBuilderMetadataKey) == true;

    /// <summary>
    /// Gets the address of an interaction's chat page.
    /// </summary>
    public static string GetChatPath(IServiceProvider services, string interactionId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.ChatInteractions, "Admin", "Chat", new RouteValueDictionary { ["itemId"] = interactionId });
}
