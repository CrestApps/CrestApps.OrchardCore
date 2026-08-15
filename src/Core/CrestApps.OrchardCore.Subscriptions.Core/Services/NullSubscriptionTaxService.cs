using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default <see cref="ISubscriptionTaxService"/> used when the Taxation feature is not enabled.
/// It applies no tax so subscriptions keep working normally without the taxation framework.
/// </summary>
public sealed class NullSubscriptionTaxService : ISubscriptionTaxService
{
    public Task ApplyTaxAsync(Invoice invoice, SubscriptionFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        invoice.TaxAmount = 0;
        invoice.TaxLines = null;
        invoice.TaxSnapshot = null;
        invoice.GrandTotal = invoice.DueNow;

        return Task.CompletedTask;
    }
}
