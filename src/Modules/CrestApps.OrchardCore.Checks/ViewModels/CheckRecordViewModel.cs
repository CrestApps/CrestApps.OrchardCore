using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Checks.ViewModels;

/// <summary>
/// The editor of a check written by hand. The amount is bound as invariant text.
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
    /// Gets or sets the payee's street.
    /// </summary>
    public string PayeeAddressLine1 { get; set; }

    /// <summary>
    /// Gets or sets the payee's second address line.
    /// </summary>
    public string PayeeAddressLine2 { get; set; }

    /// <summary>
    /// Gets or sets the payee's city.
    /// </summary>
    public string PayeeCity { get; set; }

    /// <summary>
    /// Gets or sets the payee's state or region.
    /// </summary>
    public string PayeeRegion { get; set; }

    /// <summary>
    /// Gets or sets the payee's postal code.
    /// </summary>
    public string PayeePostalCode { get; set; }

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
