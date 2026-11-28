using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The access, limits, anonymity and retention editor of a survey.
/// </summary>
public class SurveyAccessEditViewModel
{
    /// <summary>
    /// Gets or sets who may respond.
    /// </summary>
    public SurveyAudienceMode Audience { get; set; }

    /// <summary>
    /// Gets or sets the names of the chosen user audiences.
    /// </summary>
    public string[] AudienceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the user audiences to choose from, chosen ones selected. A chosen audience that no longer exists
    /// is listed too, so saving doesn't drop it unseen.
    /// </summary>
    [BindNever]
    public List<SelectListItem> AudienceOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the User Audiences feature is on.
    /// </summary>
    [BindNever]
    public bool AudiencesAvailable { get; set; }

    /// <summary>
    /// Gets or sets whether responses record who responded.
    /// </summary>
    public AnonymityMode Anonymity { get; set; }

    /// <summary>
    /// Gets or sets whether identified responses record the IP address.
    /// </summary>
    public bool CollectIpAddress { get; set; }

    /// <summary>
    /// Gets or sets when the survey opens, in local time.
    /// </summary>
    public DateTime? Opens { get; set; }

    /// <summary>
    /// Gets or sets when the survey closes, in local time.
    /// </summary>
    public DateTime? Closes { get; set; }

    /// <summary>
    /// Gets or sets whether the weekly schedule applies.
    /// </summary>
    public bool UseSchedule { get; set; }

    /// <summary>
    /// Gets or sets the rule of each weekday.
    /// </summary>
    public List<SurveyDayScheduleEditViewModel> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets what the limits count.
    /// </summary>
    public LimitCount LimitCounts { get; set; }

    /// <summary>
    /// Gets or sets the most responses in total.
    /// </summary>
    public int? LimitTotal { get; set; }

    /// <summary>
    /// Gets or sets the most responses a day.
    /// </summary>
    public int? LimitPerDay { get; set; }

    /// <summary>
    /// Gets or sets the most responses a week.
    /// </summary>
    public int? LimitPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most responses a month.
    /// </summary>
    public int? LimitPerMonth { get; set; }

    /// <summary>
    /// Gets or sets the minutes between responses.
    /// </summary>
    public int? CooldownMinutes { get; set; }

    /// <summary>
    /// Gets or sets how respondents are recognized.
    /// </summary>
    public UniquenessMode Uniqueness { get; set; }

    /// <summary>
    /// Gets or sets the most completed responses of the survey.
    /// </summary>
    public int? MaxResponses { get; set; }

    /// <summary>
    /// Gets or sets whether respondents who are not signed in pass a CAPTCHA.
    /// </summary>
    public bool RequireCaptcha { get; set; }

    /// <summary>
    /// Gets or sets the statement respondents accept before they start, such as an honor statement, or empty for none.
    /// </summary>
    public string Statement { get; set; }

    /// <summary>
    /// Gets or sets after how many idle hours an unfinished response is abandoned.
    /// </summary>
    public int IncompleteExpiryHours { get; set; } = 168;

    /// <summary>
    /// Gets or sets the minimum group size of anonymous reports.
    /// </summary>
    public int MinimumGroupSize { get; set; } = 5;

    /// <summary>
    /// Gets or sets the smallest minimum group size the site allows.
    /// </summary>
    [BindNever]
    public int MinimumGroupSizeFloor { get; set; } = SurveySettings.DefaultMinimumGroupSizeFloor;

    /// <summary>
    /// Gets or sets whether anonymity can no longer change because the survey has responses.
    /// </summary>
    [BindNever]
    public bool AnonymityLocked { get; set; }

    /// <summary>
    /// Gets or sets whether a CAPTCHA can be shown: the reCAPTCHA feature is on.
    /// </summary>
    [BindNever]
    public bool CaptchaAvailable { get; set; }

    /// <summary>
    /// Gets or sets whether the survey's anonymity secret can't be read on this server, so it fails closed until the
    /// Data Protection keys come back or the secret is reset.
    /// </summary>
    [BindNever]
    public bool SecretUnreadable { get; set; }

    /// <summary>
    /// Gets or sets the id of the survey, for the reset action.
    /// </summary>
    [BindNever]
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets after how many days responses are removed.
    /// </summary>
    public int? RetentionDays { get; set; }

    /// <summary>
    /// Gets or sets what retention does.
    /// </summary>
    public RetentionAction RetentionAction { get; set; }

    /// <summary>
    /// Gets or sets whether the survey was identified when the editor was built.
    /// </summary>
    [BindNever]
    public bool WasIdentified { get; set; }
}
