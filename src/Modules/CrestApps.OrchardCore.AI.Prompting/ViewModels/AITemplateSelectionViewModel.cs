using CrestApps.Core.Templates.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Prompting.ViewModels;

public class AITemplateSelectionViewModel
{
    public List<PromptTemplateSelectionItemViewModel> PromptTemplates { get; set; } = [];

    [BindNever]
    public IList<PromptTemplateOptionViewModel> AvailablePrompts { get; set; } = [];

    public string SelectedPromptId
    {
        get => PromptTemplates.Count > 0 ? PromptTemplates[0].TemplateId : null;
        set
        {
            if (PromptTemplates.Count == 0)
            {
                PromptTemplates.Add(new PromptTemplateSelectionItemViewModel());
            }

            PromptTemplates[0].TemplateId = value;
        }
    }

    public string PromptParameters
    {
        get => PromptTemplates.Count > 0 ? PromptTemplates[0].PromptParameters : null;
        set
        {
            if (PromptTemplates.Count == 0)
            {
                PromptTemplates.Add(new PromptTemplateSelectionItemViewModel());
            }

            PromptTemplates[0].PromptParameters = value;
        }
    }

    [BindNever]
    public Dictionary<string, string> PromptDescriptions
        => AvailablePrompts
        .Where(p => !string.IsNullOrEmpty(p.TemplateId))
        .ToDictionary(p => p.TemplateId, p => p.Description ?? string.Empty);

    [BindNever]
    public Dictionary<string, IList<TemplateParameterDescriptor>> PromptParameterDescriptors
        => AvailablePrompts
        .Where(p => !string.IsNullOrEmpty(p.TemplateId))
        .ToDictionary(p => p.TemplateId, p => p.Parameters);

    [BindNever]
    public IEnumerable<SelectListItem> AvailablePromptItems
        => AvailablePrompts.Select(p => new SelectListItem(p.Title, p.TemplateId));
}
