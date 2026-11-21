using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// Who to invite through an invitation collector.
/// </summary>
public class SurveyInviteViewModel
{
    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    [BindNever]
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the survey title.
    /// </summary>
    [BindNever]
    public string SurveyTitle { get; set; }

    /// <summary>
    /// Gets or sets the collector id.
    /// </summary>
    [BindNever]
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the collector name.
    /// </summary>
    [BindNever]
    public string CollectorName { get; set; }

    /// <summary>
    /// Gets or sets user names, one per line or comma separated.
    /// </summary>
    public string UserNames { get; set; }

    /// <summary>
    /// Gets or sets the user audience whose members are invited, or <see langword="null"/>.
    /// </summary>
    public string Audience { get; set; }

    /// <summary>
    /// Gets or sets the cohort whose members are invited, or <see langword="null"/>.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets e-mail addresses, one per line or comma separated; a line may be "Name, e-mail".
    /// </summary>
    public string Emails { get; set; }

    /// <summary>
    /// Gets or sets whether the invitations are sent at once.
    /// </summary>
    public bool SendNow { get; set; } = true;

    /// <summary>
    /// Gets or sets the audiences offered.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Audiences { get; set; } = [];

    /// <summary>
    /// Gets or sets the cohorts offered.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Cohorts { get; set; } = [];
}
