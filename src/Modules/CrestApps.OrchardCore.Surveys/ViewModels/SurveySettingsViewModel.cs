namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The site settings of Surveys.
/// </summary>
public class SurveySettingsViewModel
{
    /// <summary>
    /// Gets or sets the retention of new surveys, in days.
    /// </summary>
    public int? DefaultRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets after how many idle hours unfinished responses of new surveys are abandoned.
    /// </summary>
    public int DefaultIncompleteExpiryHours { get; set; } = 168;

    /// <summary>
    /// Gets or sets the largest upload, in megabytes.
    /// </summary>
    public int MaxUploadMegabytes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the hosts endings may redirect to, one per line.
    /// </summary>
    public string AllowedRedirectHosts { get; set; }

    /// <summary>
    /// Gets or sets the minimum group size of new anonymous surveys.
    /// </summary>
    public int DefaultMinimumGroupSize { get; set; } = 5;

    /// <summary>
    /// Gets or sets the smallest minimum group size any anonymous survey may use.
    /// </summary>
    public int MinimumGroupSizeFloor { get; set; } = CrestApps.OrchardCore.Surveys.Models.SurveySettings.DefaultMinimumGroupSizeFloor;

    /// <summary>
    /// Gets or sets the most invitation and reminder e-mails the site sends a day.
    /// </summary>
    public int InvitationEmailsPerDay { get; set; } = CrestApps.OrchardCore.Surveys.Models.SurveySettings.DefaultInvitationEmailsPerDay;
}
