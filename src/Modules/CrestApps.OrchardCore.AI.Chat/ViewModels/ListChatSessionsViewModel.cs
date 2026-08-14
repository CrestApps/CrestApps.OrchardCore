using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for list chat sessions.
/// </summary>
public class ListChatSessionsViewModel
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the chat sessions.
    /// </summary>
    public IEnumerable<AIChatSessionEntry> ChatSessions { get; set; }

    /// <summary>
    /// Gets or sets the pager.
    /// </summary>
    public IShape Pager { get; set; }

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public AIChatSessionListOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the header.
    /// </summary>
    public IShape Header { get; set; }
}
