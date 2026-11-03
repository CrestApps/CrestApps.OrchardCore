using CrestApps.Core.AI.Chat.Privacy;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Adds the names a privacy handler knows are not the customer's to the names a redaction keeps.
/// </summary>
internal static class AINamesToKeep
{
    /// <summary>
    /// Adds a name to keep, unless it is blank or kept already.
    /// </summary>
    /// <param name="context">The privacy context.</param>
    /// <param name="name">The name.</param>
    public static void Add(AIConversationPrivacyContext context, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        name = name.Trim();

        if (!context.NamesToKeep.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            context.NamesToKeep.Add(name);
        }
    }
}
