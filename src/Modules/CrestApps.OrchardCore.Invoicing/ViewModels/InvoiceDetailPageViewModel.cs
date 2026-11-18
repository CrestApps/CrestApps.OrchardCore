using CrestApps.OrchardCore.Invoicing.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The page of one invoice, built by the display drivers of <see cref="Invoice"/>.
/// </summary>
public class InvoiceDetailPageViewModel
{
    public Invoice Invoice { get; set; }

    public IShape Shape { get; set; }
}
