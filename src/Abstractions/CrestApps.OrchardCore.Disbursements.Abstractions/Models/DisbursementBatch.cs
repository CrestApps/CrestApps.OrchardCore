using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// The disbursements sent together through one provider from one of the business's bank accounts: one bank file, one
/// print run of checks. A released batch's file is kept encrypted, since it holds account numbers.
/// </summary>
public sealed class DisbursementBatch : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the provider, by name.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the business's bank account the money leaves from.
    /// </summary>
    public string SourceBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the bank account's name when the batch was made.
    /// </summary>
    public string SourceBankAccountName { get; set; }

    /// <summary>
    /// Gets or sets the date the money should arrive.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the disbursements of the batch.
    /// </summary>
    public List<string> DisbursementIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the source kind and access scope of each of its disbursements, as <c>kind/scope</c> (the scope may
    /// be empty), so the batch is shown only to people who may see all of them.
    /// </summary>
    public List<string> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the kind of record the batch's disbursements pay, or <see langword="null"/>. A batch holds the
    /// disbursements of one source kind and access scope, so who may see it is one rule.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the access scope of the batch's disbursements within <see cref="SourceKind"/>.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets the entry description a bank file prints for the batch, such as PAYROLL; empty for the bank
    /// account's own.
    /// </summary>
    public string EntryDescription { get; set; }

    /// <summary>
    /// Gets or sets when a data pipeline last delivered the batch's file.
    /// </summary>
    public DateTime? DeliveredUtc { get; set; }

    /// <summary>
    /// Gets or sets the data pipeline that last delivered the batch's file.
    /// </summary>
    public string DeliveredBy { get; set; }

    /// <summary>
    /// Gets or sets how many disbursements the batch holds.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the total amount.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public DisbursementBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the name of the file the provider wrote, when it wrote one.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets whether a file is stored.
    /// </summary>
    public bool HasFile { get; set; }

    /// <summary>
    /// Gets or sets the provider's reference of the batch.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// Gets or sets when the batch was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user name of who made it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was released.
    /// </summary>
    public DateTime? ReleasedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user name of who released it.
    /// </summary>
    public string ReleasedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
