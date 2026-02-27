using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Prompting.ViewModels;

public class AITemplateSelectionViewModel
{
    public string SelectedPromptId { get; set; }

    public string PromptParameters { get; set; }

    [BindNever]
    public IList<SelectListGroup> AvailableGroups { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> AvailablePrompts { get; set; } = [];

    [BindNever]
    public IDictionary<string, string> PromptDescriptions { get; set; } = new Dictionary<string, string>();
}
