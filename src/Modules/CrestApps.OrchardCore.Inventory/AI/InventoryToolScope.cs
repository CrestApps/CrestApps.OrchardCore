namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// What the inventory tools know about the current chat message. Every tool call of one message shares one database
/// session; when a stock change is refused because someone else changed the same stock first, the session discards its
/// pending work and writes nothing more until the message ends. The tools then refuse further changes in the message
/// instead of answering as if they had saved them.
/// </summary>
internal sealed class InventoryToolScope
{
    /// <summary>
    /// Gets or sets whether the session discarded its work in this message.
    /// </summary>
    public bool SessionClosed { get; set; }
}
