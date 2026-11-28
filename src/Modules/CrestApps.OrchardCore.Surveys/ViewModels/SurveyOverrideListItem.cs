using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One row of the accommodations list.
/// </summary>
public class SurveyOverrideListItem
{
    /// <summary>
    /// Gets or sets the accommodation.
    /// </summary>
    public SurveyOverride Override { get; set; }

    /// <summary>
    /// Gets or sets who it applies to: a user name or an audience.
    /// </summary>
    public string AppliesTo { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who granted it.
    /// </summary>
    public string GrantedBy { get; set; }
}
