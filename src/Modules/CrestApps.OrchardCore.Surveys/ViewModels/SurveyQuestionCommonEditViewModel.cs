using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The settings every question has.
/// </summary>
public class SurveyQuestionCommonEditViewModel
{
    /// <summary>
    /// Gets or sets the question text.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the placeholder.
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    /// Gets or sets whether an answer is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets whether the question keeps its place when the page shuffles.
    /// </summary>
    public bool Pinned { get; set; }

    /// <summary>
    /// Gets or sets when the question shows.
    /// </summary>
    public VisibilityMode VisibilityMode { get; set; }

    /// <summary>
    /// Gets or sets the visibility rule, as JSON.
    /// </summary>
    public string VisibilityRule { get; set; }

    /// <summary>
    /// Gets or sets the visibility formula.
    /// </summary>
    public string VisibilityFormula { get; set; }

    /// <summary>
    /// Gets or sets when an answer is required, as JSON.
    /// </summary>
    public string RequiredWhen { get; set; }

    /// <summary>
    /// Gets or sets when the question can be answered, as JSON.
    /// </summary>
    public string EnabledWhen { get; set; }

    /// <summary>
    /// Gets or sets the pool the question belongs to.
    /// </summary>
    public string PoolId { get; set; }

    /// <summary>
    /// Gets or sets whether the question type collects an answer.
    /// </summary>
    [BindNever]
    public bool Answerable { get; set; }

    /// <summary>
    /// Gets or sets the sources rules may read, as JSON.
    /// </summary>
    [BindNever]
    public string Sources { get; set; }

    /// <summary>
    /// Gets or sets the pools of the page.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> Pools { get; set; } = [];
}
