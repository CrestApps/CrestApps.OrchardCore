using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The search and filters of the invoices list.
/// </summary>
public class InvoicesAdminIndexOptions
{
    public string Search { get; set; }

    public InvoiceStatus? Status { get; set; }

    public InvoiceSettlement? Settlement { get; set; }

    public List<SelectListItem> Statuses { get; set; }

    public List<SelectListItem> Settlements { get; set; }
}
