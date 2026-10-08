using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the account a contact or an opportunity belongs to.
/// </summary>
public class AccountPickerViewModel
{
    /// <summary>
    /// Gets or sets the account content item id, or <see langword="null"/> when the item has no account.
    /// </summary>
    public string AccountContentItemId { get; set; }

    /// <summary>
    /// Gets or sets whether the picker was on the submitted form. The selector posts nothing when no account is
    /// chosen, so without this a form that never showed the picker would read as "no account" and drop it.
    /// </summary>
    public bool Rendered { get; set; }

    /// <summary>
    /// Gets or sets the account's display text.
    /// </summary>
    [BindNever]
    public string AccountDisplayText { get; set; }
}
