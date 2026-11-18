using CrestApps.OrchardCore.Addresses.Core.Editing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The editor of a check written by hand.
/// </summary>
public class CheckRecordViewModel
{
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
    /// Gets or sets the amount, bound by the invariant number binder; <see langword="null"/> when empty or unreadable.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency the amount is in, when known, which sets the input's step.
    /// </summary>
    [BindNever]
    public string Currency { get; set; }

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
