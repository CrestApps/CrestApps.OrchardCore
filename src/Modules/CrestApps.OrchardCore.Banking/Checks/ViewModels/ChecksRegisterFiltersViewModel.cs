using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The filters in the toolbar of the check register.
/// </summary>
public class ChecksRegisterFiltersViewModel
{
    /// <summary>
    /// Gets or sets the bank account filter's options.
    /// </summary>
    public IList<SelectListItem> BankAccountOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the status filter's options.
    /// </summary>
    public IList<SelectListItem> StatusOptions { get; set; } = [];

}
