using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for chat interaction capsule.
/// </summary>
public class ChatInteractionCapsuleViewModel
{
    /// <summary>
    /// Gets or sets the interaction.
    /// </summary>
    public ChatInteraction Interaction { get; set; }

    /// <summary>
    /// Gets or sets the prompts.
    /// </summary>
    [BindNever]
    public IReadOnlyCollection<ChatInteractionPrompt> Prompts { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }
}
