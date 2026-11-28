namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// What the order tools know about the current chat message: whether the database session discarded its work after
/// a refused change, so nothing more is written in the message.
/// </summary>
internal sealed class OrdersToolScope
{
    /// <summary>
    /// Gets or sets whether the session discarded its work in this message.
    /// </summary>
    public bool SessionClosed { get; set; }
}
