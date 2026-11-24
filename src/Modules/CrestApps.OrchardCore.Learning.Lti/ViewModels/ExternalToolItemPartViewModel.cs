using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Lti.ViewModels;

/// <summary>
/// The External tool part in the content editor.
/// </summary>
public class ExternalToolItemPartViewModel
{
    public string ToolId { get; set; }

    public string TargetLinkUri { get; set; }

    public string CustomParameters { get; set; }

    public decimal? ScoreMaximum { get; set; }

    public string LineItemLabel { get; set; }

    [BindNever]
    public IList<SelectListItem> Tools { get; set; } = [];

    [BindNever]
    public bool DeepLinked { get; set; }
}
