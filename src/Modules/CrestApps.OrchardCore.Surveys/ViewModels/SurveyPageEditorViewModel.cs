using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The page editor side panel.
/// </summary>
public class SurveyPageEditorViewModel
{
    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the page id.
    /// </summary>
    public string PageId { get; set; }

    /// <summary>
    /// Gets or sets the title.
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
    /// Gets or sets when the page shows.
    /// </summary>
    public VisibilityMode VisibilityMode { get; set; }

    /// <summary>
    /// Gets or sets the visibility rule, as JSON.
    /// </summary>
    public string VisibilityRule { get; set; }

    /// <summary>
    /// Gets or sets the order of the questions.
    /// </summary>
    public QuestionOrder QuestionOrder { get; set; }

    /// <summary>
    /// Gets or sets whether respondents may go back from the page.
    /// </summary>
    public bool AllowBack { get; set; } = true;

    /// <summary>
    /// Gets or sets the time limit of the page, in seconds.
    /// </summary>
    public int? TimeLimitSeconds { get; set; }

    /// <summary>
    /// Gets or sets the branches.
    /// </summary>
    public List<SurveyBranchEditViewModel> Branches { get; set; } = [];

    /// <summary>
    /// Gets or sets the pools.
    /// </summary>
    public List<SurveyPoolEditViewModel> Pools { get; set; } = [];

    /// <summary>
    /// Gets or sets the sources page rules may read, as JSON.
    /// </summary>
    [BindNever]
    public string PageRuleSources { get; set; }

    /// <summary>
    /// Gets or sets the sources branch rules may read, as JSON.
    /// </summary>
    [BindNever]
    public string BranchRuleSources { get; set; }

    /// <summary>
    /// Gets or sets the pages and endings a branch may go to.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> Targets { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may see and attach question banks.
    /// </summary>
    [BindNever]
    public bool CanUseBanks { get; set; }

    /// <summary>
    /// Gets or sets the question banks a pool may draw from.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> Banks { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the page was just saved.
    /// </summary>
    [BindNever]
    public bool Saved { get; set; }
}
