using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Agreements.ViewModels;

/// <summary>
/// The agreement reports' agreement filter.
/// </summary>
public class AgreementReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the chosen agreement; empty for every active agreement.
    /// </summary>
    public string AgreementId { get; set; }

    /// <summary>
    /// Gets or sets the agreements with covered hours to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Agreements { get; set; } = [];
}
