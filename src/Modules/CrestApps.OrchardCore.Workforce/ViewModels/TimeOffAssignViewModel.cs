namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// Assigns a policy to one worker for a range of dates.
/// </summary>
public class TimeOffAssignViewModel
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }
}
