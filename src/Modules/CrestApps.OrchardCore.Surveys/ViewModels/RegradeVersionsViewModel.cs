using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The page that scores the responses of older versions again with another version's key.
/// </summary>
public class RegradeVersionsViewModel
{
    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the survey title.
    /// </summary>
    public string SurveyTitle { get; set; }

    /// <summary>
    /// Gets or sets the ids of the versions whose responses are scored again.
    /// </summary>
    public List<string> FromVersionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the id of the version whose key is used.
    /// </summary>
    public string KeyVersionId { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the versions to pick from.
    /// </summary>
    public List<SelectListItem> Versions { get; set; } = [];
}
