using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The card of an invoice's page that sends, prints and shares it.
/// </summary>
public class InvoiceDeliveryViewModel
{
    public Invoice Invoice { get; set; }

    public string ShareUrl { get; set; }

    public bool CanSend { get; set; }
}
