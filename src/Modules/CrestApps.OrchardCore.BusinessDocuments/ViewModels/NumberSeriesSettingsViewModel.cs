using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.BusinessDocuments.ViewModels;

/// <summary>
/// The number series settings page.
/// </summary>
public class NumberSeriesSettingsViewModel
{
    /// <summary>
    /// Gets or sets the registered series, in the order they are listed.
    /// </summary>
    public List<NumberSeriesViewModel> Series { get; set; } = [];

    /// <summary>
    /// Gets or sets when the numbers can start again, to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> ResetOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets today's date in the site's time zone, as <c>yyyy-MM-dd</c>, which the examples are written with.
    /// </summary>
    [BindNever]
    public string Today { get; set; }
}
