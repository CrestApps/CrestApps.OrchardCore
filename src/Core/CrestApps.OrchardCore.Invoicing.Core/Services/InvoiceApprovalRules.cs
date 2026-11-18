using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Which invoices need approval before they are issued: a standard invoice whose total is at or above the approval
/// amount of the settings, in its own currency. Debit notes and deposit invoices made by the site do not.
/// </summary>
public static class InvoiceApprovalRules
{
    /// <summary>
    /// Returns whether an invoice, with its totals worked out, needs approval to be issued.
    /// </summary>
    public static bool Requires(Invoice invoice, InvoicingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return settings?.ApprovalThreshold is { } threshold &&
            invoice.Kind == InvoiceKind.Standard &&
            (invoice.Totals?.Total ?? 0m) >= threshold;
    }
}
