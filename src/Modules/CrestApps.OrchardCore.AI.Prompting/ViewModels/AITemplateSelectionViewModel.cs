using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Prompting.ViewModels;

public class AITemplateSelectionViewModel
{
    public List<PromptTemplateSelectionItemViewModel> PromptTemplates { get; set; } = [];

    [BindNever]
    public IList<PromptTemplateOptionViewModel> AvailablePrompts { get; set; } = [];
}
