namespace CrestApps.OrchardCore.AI.Chat.Interactions.Models;

/// <summary>
/// One session in the AI Profile Builder session list: the session and the title of the chat interaction that
/// carries its conversation. The admin list builds a row for each.
/// </summary>
public sealed class ProfileBuilderSessionEntry
{
    /// <summary>
    /// Gets or sets the session.
    /// </summary>
    public ProfileBuilderSession Session { get; set; }

    /// <summary>
    /// Gets or sets the session's title.
    /// </summary>
    public string Title { get; set; }
}
