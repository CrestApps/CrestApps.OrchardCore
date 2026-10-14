using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Computes the tax portion of a refund from an original transaction's immutable
/// <see cref="TaxSnapshot"/>. Refund tax is always derived from the historical snapshot and never
/// recalculated with current tax rules, so a rate change after the original purchase never alters the
/// refund. This is the single authoritative way to refund tax; refunds must not introduce a second tax
/// calculation.
/// </summary>
public interface ITaxRefundCalculator
{
    /// <summary>
    /// Computes a full refund of the tax captured on the supplied snapshot.
    /// </summary>
    /// <param name="snapshot">The original transaction's tax snapshot.</param>
    /// <returns>The refunded tax, equal to the amounts captured on the snapshot.</returns>
    TaxRefundResult CalculateFullRefund(TaxSnapshot snapshot);

    /// <summary>
    /// Computes a partial refund proportionally from the supplied snapshot. The proportion is the
    /// requested <paramref name="refundTotalAmount"/> relative to the snapshot's total amount, and it is
    /// allocated across the original tax lines so each jurisdiction is refunded according to the original
    /// determination.
    /// </summary>
    /// <param name="snapshot">The original transaction's tax snapshot.</param>
    /// <param name="refundTotalAmount">The gross amount being refunded (taxable base plus any tax added on top).</param>
    /// <returns>The proportional refunded tax.</returns>
    TaxRefundResult CalculateProportionalRefund(TaxSnapshot snapshot, decimal refundTotalAmount);
}
