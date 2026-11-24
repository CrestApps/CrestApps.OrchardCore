using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// A print run of queued checks on pre-printed stock: the bank account, the checks, the layout and the number on the
/// first sheet; then, after printing, the sheets that misprinted.
/// </summary>
public class CheckPrintRunViewModel
{
    /// <summary>
    /// Gets or sets the bank account whose queued checks print.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the layout the checks print with.
    /// </summary>
    public string LayoutId { get; set; }

    /// <summary>
    /// Gets or sets the number printed on the first sheet, as text.
    /// </summary>
    public string FirstNumber { get; set; }

    /// <summary>
    /// Gets or sets the checks of the run, in the order they print.
    /// </summary>
    public string[] CheckIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the checks whose sheets misprinted.
    /// </summary>
    public string[] MisprintedCheckIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the print run's id, made when the run is confirmed for printing.
    /// </summary>
    public string PrintRunId { get; set; }

    /// <summary>
    /// Gets or sets the name of the bank account.
    /// </summary>
    [BindNever]
    public string BankAccountName { get; set; }

    /// <summary>
    /// Gets or sets the checks shown: the account's queued checks, or the run's checks in the order they print.
    /// </summary>
    [BindNever]
    public IReadOnlyList<CheckRecord> Checks { get; set; } = [];

    /// <summary>
    /// Gets or sets the number printed on the first sheet, once it was checked.
    /// </summary>
    [BindNever]
    public long First { get; set; }

    /// <summary>
    /// Gets or sets the business's bank accounts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BankAccountOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the layouts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> LayoutOptions { get; set; } = [];
}
