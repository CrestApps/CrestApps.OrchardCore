namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// Plaid's answer that holds an item, such as <c>/item/webhook/update</c>.
/// </summary>
internal sealed class PlaidItemResponse
{
    /// <summary>
    /// Gets or sets the item.
    /// </summary>
    public PlaidItemInfo Item { get; set; }
}
