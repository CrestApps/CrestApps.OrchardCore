namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// The signal that drops the product feeds a tenant keeps, so they are built again on their next read. Raise it with
/// Orchard Core's <c>ISignal</c> when something feeds show changes, such as a product's stock.
/// </summary>
public static class ProductFeedCache
{
    /// <summary>
    /// The key of the signal.
    /// </summary>
    public const string SignalKey = "CrestApps.OrchardCore.Products.Feeds";
}
