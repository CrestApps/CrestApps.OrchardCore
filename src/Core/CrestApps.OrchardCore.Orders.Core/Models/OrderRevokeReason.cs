namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// Why what an order gave its buyer is taken back.
/// </summary>
public enum OrderRevokeReason
{
    /// <summary>
    /// The order was canceled.
    /// </summary>
    Canceled = 1,

    /// <summary>
    /// The order was refunded in full.
    /// </summary>
    Refunded = 2,
}
