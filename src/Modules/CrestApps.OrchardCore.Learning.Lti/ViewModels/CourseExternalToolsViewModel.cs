using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Lti.ViewModels;

/// <summary>
/// The External tools card of the course editor: the tools that offer Deep Linking.
/// </summary>
public class CourseExternalToolsViewModel
{
    public string CourseId { get; set; }

    public IList<SelectListItem> Tools { get; set; } = [];
}
