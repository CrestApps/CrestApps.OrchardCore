using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The check register page: its filters, the checks of the page and the layouts to print with.
/// </summary>
public class ChecksIndexViewModel
{
    /// <summary>
    /// Gets or sets the payee name or check number searched for.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the bank account shown; all when empty.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the status shown; all when empty.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the bulk action chosen (the layout to print the checked checks with) and the actions offered.
    /// </summary>
    public CatalogEntryOptions<string> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the checks of the page.
    /// </summary>
    [BindNever]
    public AdminListContext<CheckRecord> List { get; set; }

    /// <summary>
    /// Gets or sets the bank account filter's options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BankAccountOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the status filter's options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];


    /// <summary>
    /// Gets or sets whether the user may issue and print checks.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }
}
