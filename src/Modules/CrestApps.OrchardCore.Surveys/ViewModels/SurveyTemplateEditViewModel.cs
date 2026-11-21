using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The form of a survey template.
/// </summary>
public class SurveyTemplateEditViewModel
{
    /// <summary>
    /// Gets or sets the id of the template, or <see langword="null"/> for a new one.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the kind of survey the template makes.
    /// </summary>
    public string Kind { get; set; } = SurveyKinds.Survey;

    /// <summary>
    /// Gets or sets the Font Awesome class of the icon.
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-clipboard-list";

    /// <summary>
    /// Gets or sets the position in the list.
    /// </summary>
    public int Position { get; set; } = 100;

    /// <summary>
    /// Gets or sets the id of a survey whose draft the template copies, or <see langword="null"/>.
    /// </summary>
    public string FromSurveyId { get; set; }

    /// <summary>
    /// Gets or sets the definition as JSON.
    /// </summary>
    public string DefinitionJson { get; set; }

    /// <summary>
    /// Gets or sets the number of questions of the definition.
    /// </summary>
    [BindNever]
    public int QuestionCount { get; set; }

    /// <summary>
    /// Gets or sets the kinds offered.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the surveys a new template may copy.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Surveys { get; set; } = [];

    /// <summary>
    /// Gets whether the template is new.
    /// </summary>
    public bool IsNew => string.IsNullOrEmpty(Id);
}
