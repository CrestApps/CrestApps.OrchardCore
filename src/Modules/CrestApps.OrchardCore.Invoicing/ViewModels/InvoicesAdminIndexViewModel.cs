using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The invoices list.
/// </summary>
public class InvoicesAdminIndexViewModel
{
    public InvoicesAdminIndexOptions Options { get; set; } = new();

    public AdminListContext List { get; set; }

    public bool CanCreate { get; set; }
}
