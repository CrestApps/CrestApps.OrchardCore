using System;
using System.Collections.Generic;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// An immutable capture of a tax determination that is stored with a transaction. A snapshot lets the
/// system reproduce and audit the original tax without recalculating it with current rules.
/// </summary>
public sealed class TaxSnapshot
{
    /// <summary>
    /// Gets or sets the UTC date the snapshot was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the transaction date, in UTC, used when the tax was determined.
    /// </summary>
    public DateTime TransactionDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the currency the amounts are expressed in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total taxable base captured at determination time.
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>
    /// Gets or sets the total tax captured at determination time.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the total amount captured at determination time.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the tax lines captured at determination time.
    /// </summary>
    public IList<TaxLine> Lines { get; set; } = [];
}
