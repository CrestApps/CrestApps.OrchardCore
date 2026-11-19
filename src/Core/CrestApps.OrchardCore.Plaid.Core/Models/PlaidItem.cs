using CrestApps.OrchardCore.SensitiveData;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// A Plaid item the site keeps because a linked bank account still depends on it: its account number is one the bank
/// made for Plaid, which stops working if the person takes back Plaid's access. Items nothing depends on are removed
/// as soon as the numbers are read, so most linked accounts have none.
/// </summary>
public sealed class PlaidItem
{
    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the item.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the account the bank account was linked to.
    /// </summary>
    public string PlaidAccountId { get; set; }

    /// <summary>
    /// Gets or sets the id of the bank account.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the access token, protected with <see cref="PlaidConstants.AccessTokenPurpose"/>.
    /// </summary>
    public ProtectedValue AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the environment the item belongs to.
    /// </summary>
    public PlaidEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets when the item was linked.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
