using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The publish page.
/// </summary>
public class SurveyPublishViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    [BindNever]
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the problems and changes.
    /// </summary>
    [BindNever]
    public SurveyPublishPreview Preview { get; set; }

    /// <summary>
    /// Gets or sets the publish notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets whether the author confirms changes that affect existing answers.
    /// </summary>
    public bool ConfirmDestructive { get; set; }
}
