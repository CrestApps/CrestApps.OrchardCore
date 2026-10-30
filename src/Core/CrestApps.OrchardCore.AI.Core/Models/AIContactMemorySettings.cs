namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// How the AI rewrites a contact's memory note after each conversation.
/// </summary>
public sealed class AIContactMemorySettings
{
    /// <summary>
    /// Gets or sets the instructions the memory writer follows, or <see langword="null"/> for the built-in ones.
    /// </summary>
    public string Instructions { get; set; }
}
