using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;

/// <summary>
/// The YesSql index used to find AI Profile Builder sessions.
/// </summary>
public sealed class ProfileBuilderSessionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the identifier of the chat interaction that carries the session's conversation.
    /// </summary>
    public string ChatInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the session.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets when the session was last changed, or created when it never was.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
