using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Agreements.Core.Indexes;

/// <summary>
/// Indexes agreements by number, status and account.
/// </summary>
public sealed class AgreementIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }
}
