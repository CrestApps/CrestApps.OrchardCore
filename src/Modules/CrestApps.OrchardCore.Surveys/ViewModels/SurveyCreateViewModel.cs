using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The new survey form.
/// </summary>
public class SurveyCreateViewModel
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [Required]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the technical name, or empty to derive one.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the template.
    /// </summary>
    public string Template { get; set; } = "blank";

    /// <summary>
    /// Gets or sets the templates offered.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SurveyTemplateDescriptor> Templates { get; set; } = [];
}
