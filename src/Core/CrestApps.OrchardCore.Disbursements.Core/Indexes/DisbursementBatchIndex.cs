using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Disbursements.Core.Indexes;

/// <summary>
/// Finds disbursement batches by provider, status, source account and dates.
/// </summary>
public sealed class DisbursementBatchIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the batch's disbursements pay.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the source kind and access scope, so lists read only the batches a person may act on.
    /// </summary>
    public string AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the business's bank account the money leaves from.
    /// </summary>
    public string SourceBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the date the money should arrive, at midnight.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was released, or <see langword="null"/>.
    /// </summary>
    public DateTime? ReleasedUtc { get; set; }
}
