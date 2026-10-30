using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Where the privacy task is in each profile's conversations.
/// </summary>
public sealed class AIConversationPrivacyState : Document
{
    /// <summary>
    /// Gets the last activity of the last conversation the storage mode was applied to, by profile.
    /// </summary>
    public Dictionary<string, DateTime> Cursors { get; init; } = new(StringComparer.Ordinal);
}
