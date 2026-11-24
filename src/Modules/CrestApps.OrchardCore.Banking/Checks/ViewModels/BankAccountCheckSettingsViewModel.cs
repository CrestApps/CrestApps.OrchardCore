using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The Checks card on one of the business's bank accounts.
/// </summary>
public class BankAccountCheckSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the card was on the posted form. A form drawn while the account was not
    /// the business's has no card, and its empty field must not clear the setting.
    /// </summary>
    public bool Shown { get; set; }

    /// <summary>
    /// Gets or sets the layout the account's checks print with when the person printing doesn't choose one.
    /// </summary>
    public string DefaultLayoutId { get; set; }

    /// <summary>
    /// Gets or sets the layouts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> LayoutOptions { get; set; } = [];
}
