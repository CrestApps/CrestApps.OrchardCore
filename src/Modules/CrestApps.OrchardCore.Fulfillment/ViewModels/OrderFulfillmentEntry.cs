using CrestApps.OrchardCore.Fulfillment.Core.Models;

namespace CrestApps.OrchardCore.Fulfillment.ViewModels;

/// <summary>
/// One partner's row on an order's fulfillment card.
/// </summary>
public class OrderFulfillmentEntry
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public FulfillmentRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the partner's name shown to people.
    /// </summary>
    public string DisplayName { get; set; }
}
