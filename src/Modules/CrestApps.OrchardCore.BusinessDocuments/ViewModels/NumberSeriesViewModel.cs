using CrestApps.OrchardCore.Numbering;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.BusinessDocuments.ViewModels;

/// <summary>
/// One series on the number series settings page.
/// </summary>
public class NumberSeriesViewModel
{
    /// <summary>
    /// Gets or sets the name of the series, such as <c>Financial.Invoice</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the pattern the numbers are written with.
    /// </summary>
    public string Pattern { get; set; }

    /// <summary>
    /// Gets or sets the first number of the series.
    /// </summary>
    public long FirstNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets when the numbers start again.
    /// </summary>
    public NumberSeriesReset Reset { get; set; }

    /// <summary>
    /// Gets or sets the title of the series.
    /// </summary>
    [BindNever]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the pattern the series was registered with.
    /// </summary>
    [BindNever]
    public string DefaultPattern { get; set; }

    /// <summary>
    /// Gets or sets an example of a number of the series, written with today's date.
    /// </summary>
    [BindNever]
    public string Example { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the site chose a format of its own for the series.
    /// </summary>
    [BindNever]
    public bool IsCustomized { get; set; }
}
