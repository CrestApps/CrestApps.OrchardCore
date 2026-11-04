using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// The editor of a Skills workflow event.
/// </summary>
public class SkillsEventActivityViewModel
{
    /// <summary>
    /// Gets or sets the certifications that start the workflow.
    /// </summary>
    public string[] CertificationIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the event can be limited to some certifications.
    /// </summary>
    [BindNever]
    public bool HasCertificationFilter { get; set; }

    /// <summary>
    /// Gets or sets the certifications to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CertificationOptions { get; set; } = [];
}
