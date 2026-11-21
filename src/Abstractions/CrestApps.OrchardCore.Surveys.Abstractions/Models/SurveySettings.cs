namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The site settings of Surveys.
/// </summary>
public sealed class SurveySettings
{
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
}
