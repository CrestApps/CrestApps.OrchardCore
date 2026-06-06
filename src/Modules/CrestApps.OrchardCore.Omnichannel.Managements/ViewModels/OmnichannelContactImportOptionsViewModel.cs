using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// View model for the omnichannel contact import options UI.
/// </summary>
public class OmnichannelContactImportOptionsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore duplicate contacts based on phone number.
    /// </summary>
    public bool IgnoreDuplicateByPhoneNumber { get; set; } = true;

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code used to normalize imported phone numbers.
    /// </summary>
    public string SelectedCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the available countries for phone-number normalization.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableCountries { get; set; } = [];
}
