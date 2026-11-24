using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The editor of a check written by hand. The amount is bound as invariant text.
/// </summary>
public class CheckRecordViewModel
{
    /// <summary>
    /// Gets or sets whether the check is being written; <see langword="false"/> while a check waiting to print is edited,
    /// which keeps its bank account and number.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the name of the bank account of a check being edited.
    /// </summary>
    [BindNever]
    public string BankAccountName { get; set; }

    /// <summary>
    /// Gets or sets the number of a check being edited, or 0 while it waits for a print run to number it.
    /// </summary>
    [BindNever]
    public long CheckNumber { get; set; }

    /// <summary>
    /// Gets or sets the bank account the check is drawn on.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets who the check is paid to.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the payee's address, in the shared address editor.
    /// </summary>
    public AddressEditorViewModel PayeeAddress { get; set; } = new();

    /// <summary>
    /// Gets or sets the address editor's shape.
    /// </summary>
    [BindNever]
    public IShape PayeeAddressEditor { get; set; }

    /// <summary>
    /// Gets or sets how the check gets its number.
    /// </summary>
    public CheckNumbering Numbering { get; set; }

    /// <summary>
    /// Gets or sets the number of a check written by hand, as text.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the ways a check can get its number.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> NumberingOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the amount, as invariant text.
    /// </summary>
    public string Amount { get; set; }

    /// <summary>
    /// Gets or sets the check date, as yyyy-MM-dd.
    /// </summary>
    public string CheckDate { get; set; }

    /// <summary>
    /// Gets or sets the memo.
    /// </summary>
    public string Memo { get; set; }

    /// <summary>
    /// Gets or sets the business's bank accounts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BankAccountOptions { get; set; } = [];
}
