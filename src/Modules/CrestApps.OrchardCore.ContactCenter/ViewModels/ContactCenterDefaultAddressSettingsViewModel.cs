using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Edits the default phone and SMS numbers.
/// </summary>
public class ContactCenterDefaultAddressSettingsViewModel
{
    /// <summary>
    /// Gets or sets the address of the default phone number.
    /// </summary>
    public string DefaultPhoneAddressId { get; set; }

    /// <summary>
    /// Gets or sets the address of the default SMS number.
    /// </summary>
    public string DefaultSmsAddressId { get; set; }

    /// <summary>
    /// Gets or sets the addresses used for calls.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PhoneAddressOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the addresses used for texts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> SmsAddressOptions { get; set; } = [];
}
