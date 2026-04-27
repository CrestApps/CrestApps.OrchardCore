using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for chat session.
/// </summary>
public class ChatSessionViewModel
{
    /// <summary>
    /// Gets or sets the session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public IShape Content { get; set; }

    /// <summary>
    /// Gets or sets the history.
    /// </summary>
    public IList<IShape> History { get; set; }
}
