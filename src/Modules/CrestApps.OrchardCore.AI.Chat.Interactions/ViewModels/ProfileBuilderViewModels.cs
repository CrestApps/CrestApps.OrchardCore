using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// The AI Profile Builder session list.
/// </summary>
public class ProfileBuilderIndexViewModel : ListCatalogEntryViewModel<ProfileBuilderSessionEntry>
{
    /// <summary>
    /// Gets or sets the chat profiles a session can start from.
    /// </summary>
    public IReadOnlyList<SelectListItem> Profiles { get; set; } = [];
}

/// <summary>
/// The AI Profile Builder session page.
/// </summary>
public class ProfileBuilderSessionViewModel
{
    /// <summary>
    /// Gets or sets the session.
    /// </summary>
    public ProfileBuilderSession Session { get; set; }

    /// <summary>
    /// Gets or sets the session's chat interaction.
    /// </summary>
    public ChatInteraction Interaction { get; set; }

    /// <summary>
    /// Gets or sets the session's title.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the chat interaction editor shape.
    /// </summary>
    public dynamic Editor { get; set; }

    /// <summary>
    /// Gets or sets the AI profile the session is linked to, if any.
    /// </summary>
    public AIProfile Profile { get; set; }
}
