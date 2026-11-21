using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.AI.ViewModels;

/// <summary>
/// The page that translates a survey with AI.
/// </summary>
public class SurveyAITranslateViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the site's cultures, without the default one.
    /// </summary>
    public List<SelectListItem> Cultures { get; set; } = [];

    /// <summary>
    /// Gets or sets the culture to translate into.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets whether existing translations are replaced.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// Gets or sets whether an AI model is set up.
    /// </summary>
    public bool ModelAvailable { get; set; }
}
