
namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// What Plaid says about an item: one sign-in at one bank, which may hold several accounts.
/// </summary>
public sealed class PlaidItemInfo
{
    /// <summary>
    /// Gets or sets Plaid's id of the item.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the bank, or <see langword="null"/>.
    /// </summary>
    public string InstitutionId { get; set; }

    /// <summary>
    /// Gets or sets the URL Plaid sends the item's webhooks to, or <see langword="null"/>.
    /// </summary>
    public string Webhook { get; set; }

    /// <summary>
    /// Gets or sets the item's error, such as <c>ITEM_LOGIN_REQUIRED</c>, or <see langword="null"/>.
    /// </summary>
    public PlaidError Error { get; set; }
}
