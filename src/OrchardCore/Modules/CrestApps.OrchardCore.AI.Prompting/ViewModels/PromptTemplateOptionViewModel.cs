using CrestApps.AI.Prompting.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Prompting.ViewModels;

public class PromptTemplateOptionViewModel
{
    public string TemplateId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Category { get; set; }

    [BindNever]
    public IList<AITemplateParameterDescriptor> Parameters { get; set; } = [];
}
