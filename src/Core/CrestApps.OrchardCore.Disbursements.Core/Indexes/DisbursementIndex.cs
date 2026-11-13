using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Disbursements.Core.Indexes;

/// <summary>
/// Finds disbursements by status, method, payee, source, idempotency key, trace number, batch and dates. It keeps no
/// amount: the register and the data set read amounts from the documents.
/// </summary>
public sealed class DisbursementIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the method.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the payee's name.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the business's bank account the money leaves from.
    /// </summary>
    public string SourceBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the payee's bank account.
    /// </summary>
    public string DestinationBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the kind of record it pays.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record it pays.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the source kind and access scope, so lists read only the disbursements a person may see.
    /// </summary>
    public string AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the trace number the bank knows it by.
    /// </summary>
    public string TraceNumber { get; set; }

    /// <summary>
    /// Gets or sets the check that pays it.
    /// </summary>
    public string CheckId { get; set; }

    /// <summary>
    /// Gets or sets the batch it was sent in.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
