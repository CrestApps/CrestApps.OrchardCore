using CrestApps.Core.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for list chat interactions.
/// </summary>
public class ListChatInteractionsViewModel
{
    /// <summary>
    /// Gets or sets the interactions.
    /// </summary>
    public IEnumerable<ChatInteraction> Interactions { get; set; }

    /// <summary>
    /// Gets or sets the pager.
    /// </summary>
    public IShape Pager { get; set; }

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public ChatInteractionListOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the header.
    /// </summary>
    public IShape Header { get; set; }
}
