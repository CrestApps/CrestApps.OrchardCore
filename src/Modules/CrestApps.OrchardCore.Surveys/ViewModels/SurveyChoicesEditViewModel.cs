using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The choice list of a question.
/// </summary>
public class SurveyChoicesEditViewModel
{
    /// <summary>
    /// Gets or sets the choices.
    /// </summary>
    public List<SurveyChoice> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets the visibility rule of each choice, as JSON, in the order of <see cref="Choices"/>.
    /// </summary>
    public List<string> ChoiceRules { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the choices show in a random order.
    /// </summary>
    public bool ShuffleChoices { get; set; }

    /// <summary>
    /// Gets or sets the question whose choices are carried forward.
    /// </summary>
    public string ChoicesFrom { get; set; }

    /// <summary>
    /// Gets or sets which choices are carried forward.
    /// </summary>
    public ChoicesFromMode ChoicesFromMode { get; set; }

    /// <summary>
    /// Gets or sets whether the question type supports an other choice.
    /// </summary>
    [BindNever]
    public bool SupportsOther { get; set; }

    /// <summary>
    /// Gets or sets the sources choice rules may read, as JSON.
    /// </summary>
    [BindNever]
    public string Sources { get; set; }

    /// <summary>
    /// Gets or sets the earlier choice questions choices may be carried forward from.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> CarryForwardSources { get; set; } = [];
}
