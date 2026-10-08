namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Represents the edit view model for the <c>ScheduleDialerRetryTask</c> workflow activity.
/// </summary>
public class ScheduleDialerRetryTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the completed dialer activity to try again.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves how many minutes to wait before the next attempt.
    /// </summary>
    public string DelayMinutes { get; set; }
}
