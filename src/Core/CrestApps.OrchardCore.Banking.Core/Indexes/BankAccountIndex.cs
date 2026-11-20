using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Banking.Core.Indexes;

/// <summary>
/// Finds bank accounts by name, owner and status. It holds no number.
/// </summary>
public sealed class BankAccountIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the account's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of its owner; empty for the business's own accounts.
    /// </summary>
    public string OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the id of its owner in its kind; empty for the business's own accounts.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets its status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets its country.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets its currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
