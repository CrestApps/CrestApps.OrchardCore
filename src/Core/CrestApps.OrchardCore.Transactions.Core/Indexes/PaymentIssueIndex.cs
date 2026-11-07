using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="PaymentIssue"/>.
/// </summary>
public sealed class PaymentIssueIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets what failed.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets whether it is still open.
    /// </summary>
    public PaymentIssueStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
