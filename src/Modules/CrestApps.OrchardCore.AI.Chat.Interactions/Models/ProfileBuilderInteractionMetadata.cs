namespace CrestApps.OrchardCore.AI.Chat.Interactions.Models;

/// <summary>
/// Marks a chat interaction as the conversation of an AI Profile Builder session.
/// </summary>
/// <remarks>
/// The Chat Interactions list leaves these out: they are listed, opened and deleted from the builder, which also
/// owns the feedback ledger that belongs to them.
/// </remarks>
public sealed class ProfileBuilderInteractionMetadata
{
    /// <summary>
    /// Gets or sets the identifier of the builder session.
    /// </summary>
    public string SessionId { get; set; }
}
