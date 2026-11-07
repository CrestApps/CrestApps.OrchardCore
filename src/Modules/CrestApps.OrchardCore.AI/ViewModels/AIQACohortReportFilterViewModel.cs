using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The QA cohort filter of the QA dashboard report.
/// </summary>
public class AIQACohortReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the chosen QA cohort identifier, or empty for every scored conversation.
    /// </summary>
    public string QACohortId { get; set; }

    /// <summary>
    /// Gets or sets the QA cohorts to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Cohorts { get; set; } = [];
}
