namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The site settings of Surveys.
/// </summary>
public sealed class SurveySettings
{
    /// <summary>
    /// The floor of the minimum group size of anonymous surveys when the site sets none.
    /// </summary>
    public const int DefaultMinimumGroupSizeFloor = 5;

    /// <summary>
    /// The most invitation and reminder e-mails the site sends a day when the site sets none.
    /// </summary>
    public const int DefaultInvitationEmailsPerDay = 2000;

    /// <summary>
    /// Gets or sets the retention of new surveys, in days, or null to keep responses.
    /// </summary>
    public int? DefaultRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets after how many idle hours unfinished responses of new surveys are abandoned.
    /// </summary>
    public int DefaultIncompleteExpiryHours { get; set; } = 168;

    /// <summary>
    /// Gets or sets the largest file a respondent may upload, in megabytes.
    /// </summary>
    public int MaxUploadMegabytes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the hosts endings may redirect to besides this site.
    /// </summary>
    public List<string> AllowedRedirectHosts { get; set; } = [];

    /// <summary>
    /// Gets or sets the minimum group size of new anonymous surveys.
    /// </summary>
    public int DefaultMinimumGroupSize { get; set; } = 5;

    /// <summary>
    /// Gets or sets the smallest minimum group size any anonymous survey may use. A survey may ask for more, never for
    /// less, and the floor applies to surveys saved before it was raised. At least 1.
    /// </summary>
    public int MinimumGroupSizeFloor { get; set; } = DefaultMinimumGroupSizeFloor;

    /// <summary>
    /// Gets or sets the most survey invitation and reminder e-mails the site sends in a day (from midnight in the site's
    /// time zone), across all surveys. Invitations to users of the site go to their notifications and don't count. An
    /// invitation over the limit is held back and the hourly reminder task sends it on a later day; a reminder over it
    /// stays due. At least 1.
    /// </summary>
    public int InvitationEmailsPerDay { get; set; } = DefaultInvitationEmailsPerDay;
}
