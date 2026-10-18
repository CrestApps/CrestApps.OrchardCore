namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// What the store is doing to an order in this request, for the handlers it reaches on the way: who is doing it, so
/// a refund recorded when the payment comes back says who gave it, and whether stock goes back.
/// </summary>
public sealed class OrderOperationScope
{
    /// <summary>
    /// Gets or sets the name of who is acting on the order, or <see langword="null"/> when nobody is.
    /// </summary>
    public string ActorName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether what the order sold goes back into stock once everything is refunded.
    /// </summary>
    public bool Restock { get; set; }
}
