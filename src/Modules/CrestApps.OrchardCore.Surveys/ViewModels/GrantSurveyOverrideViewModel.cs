using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The form that grants an accommodation.
/// </summary>
public class GrantSurveyOverrideViewModel
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
    /// Gets or sets the user name of the person it applies to.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the audience it applies to.
    /// </summary>
    public string AudienceId { get; set; }

    /// <summary>
    /// Gets or sets the kind of the record it is limited to, such as LearningEnrollment.
    /// </summary>
    public string ContextKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record it is limited to.
    /// </summary>
    public string ContextId { get; set; }

    /// <summary>
    /// Gets or sets the extra minutes.
    /// </summary>
    public int ExtraMinutes { get; set; }

    /// <summary>
    /// Gets or sets the extra attempts.
    /// </summary>
    public int ExtraAttempts { get; set; }

    /// <summary>
    /// Gets or sets another opening time, in local time.
    /// </summary>
    public DateTime? Opens { get; set; }

    /// <summary>
    /// Gets or sets another closing time, in local time.
    /// </summary>
    public DateTime? Closes { get; set; }

    /// <summary>
    /// Gets or sets why it is granted.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the audiences to pick from.
    /// </summary>
    public List<SelectListItem> Audiences { get; set; } = [];
}
