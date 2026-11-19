namespace CrestApps.OrchardCore.Fulfillment.ViewModels;

/// <summary>
/// The fulfillment partners whose features are on, and whether each one is connected.
/// </summary>
public class FulfillmentPartnersViewModel
{
    /// <summary>
    /// Gets the partners, in name order.
    /// </summary>
    public IList<FulfillmentPartnerEntry> Partners { get; } = [];
}
