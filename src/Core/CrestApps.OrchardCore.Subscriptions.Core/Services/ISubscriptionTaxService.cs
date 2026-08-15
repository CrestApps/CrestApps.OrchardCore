using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Applies taxation to a subscription <see cref="Invoice"/>. The subscription module never calculates
/// tax itself; it consumes the taxation framework through this seam. When the Taxation feature is
/// disabled the registered implementation is a no-op so subscriptions keep working normally.
/// </summary>
public interface ISubscriptionTaxService
{
    /// <summary>
    /// Determines the tax for the amounts that are due now on the supplied invoice, sets
    /// <see cref="Invoice.TaxAmount"/>, <see cref="Invoice.TaxLines"/>, <see cref="Invoice.TaxSnapshot"/>,
    /// and recomputes <see cref="Invoice.GrandTotal"/>.
    /// </summary>
    /// <param name="invoice">The invoice to tax. Its <see cref="Invoice.DueNow"/> is expected to be set.</param>
    /// <param name="flow">The subscription flow that describes the transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ApplyTaxAsync(Invoice invoice, SubscriptionFlow flow, CancellationToken cancellationToken = default);
}
