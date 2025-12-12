using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// ViewModel for managing custom chat instances.
/// </summary>
public class ManageCustomChatInstancesViewModel
{
    /// <summary>
    /// Gets or sets the current session being edited/viewed.
    /// </summary>
    public AIChatSession CurrentSession { get; set; }

    /// <summary>
    /// Gets or sets the configuration for the current instance.
    /// </summary>
    public CustomChatInstanceViewModel Configuration { get; set; }

    /// <summary>
    /// Gets or sets the list of all user's custom chat instances.
    /// </summary>
    public IList<AIChatSession> Instances { get; set; } = [];

    /// <summary>
    /// Gets or sets the chat content shape.
    /// </summary>
    public IShape ChatContent { get; set; }

    public IList<IShape> History { get; set; }

    /// <summary>
    /// Gets or sets whether this is a new instance.
    /// </summary>
    public bool IsNew { get; set; }
}
