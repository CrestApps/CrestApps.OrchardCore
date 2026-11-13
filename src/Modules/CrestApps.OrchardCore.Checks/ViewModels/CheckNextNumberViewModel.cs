using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Checks.ViewModels;

/// <summary>
/// The page that sets the next check number of a bank account, such as when new stock is loaded.
/// </summary>
public class CheckNextNumberViewModel
{
    /// <summary>
    /// Gets or sets the bank account.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the next number, as text.
    /// </summary>
    public string NextNumber { get; set; }

    /// <summary>
    /// Gets or sets the next number of each bank account now, for display.
    /// </summary>
    [BindNever]
    public IList<(string Name, long Next)> Current { get; set; } = [];

    /// <summary>
    /// Gets or sets the business's bank accounts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BankAccountOptions { get; set; } = [];
}
