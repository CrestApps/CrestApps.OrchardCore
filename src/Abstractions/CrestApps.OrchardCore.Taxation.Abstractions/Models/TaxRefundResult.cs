using System.Collections.Generic;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// The outcome of computing the tax portion of a refund from an original transaction's
/// <see cref="TaxSnapshot"/>. The amounts are derived from the historical snapshot and are never
/// recalculated with current tax rules, so historical transactions remain authoritative.
/// </summary>
public sealed class TaxRefundResult
{
    /// <summary>
    /// Gets or sets the currency the amounts are expressed in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the portion of the original taxable base that is being refunded.
    /// </summary>
    public decimal RefundedTaxableAmount { get; set; }

    /// <summary>
    /// Gets or sets the portion of the original tax that is being refunded.
    /// </summary>
    public decimal RefundedTaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the total amount being refunded (taxable base plus tax that was added on top).
    /// </summary>
    public decimal RefundedTotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the per-line refunded tax, allocated from the original snapshot's lines so each
    /// jurisdiction/tax is refunded according to the original determination rather than current rules.
    /// </summary>
    public IList<TaxLine> Lines { get; set; } = [];
}
