using CrestApps.Core.Templates.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Prompting.ViewModels;

/// <summary>
/// Represents the view model for prompt template option.
/// </summary>
public class PromptTemplateOptionViewModel
{
    /// <summary>
    /// Gets or sets the template id.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    [BindNever]
    public IList<TemplateParameterDescriptor> Parameters { get; set; } = [];
}
