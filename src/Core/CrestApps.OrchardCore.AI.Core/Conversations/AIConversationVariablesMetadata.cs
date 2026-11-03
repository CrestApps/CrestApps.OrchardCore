namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The conversation variables kept on an AI session: named text values that workflows, data extraction and the
/// channel set, and that the system message, tools and workflows read.
/// </summary>
public sealed class AIConversationVariablesMetadata
{
    /// <summary>
    /// Gets or sets the variables by name.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
