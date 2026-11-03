namespace CrestApps.OrchardCore.Scheduling.Deployments;

/// <summary>
/// The settings of the Scheduling deployment step.
/// </summary>
public class SchedulingDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets whether the appointments are exported too.
    /// </summary>
    public bool IncludeAppointments { get; set; }

    /// <summary>
    /// Gets or sets whether the waitlist is exported too.
    /// </summary>
    public bool IncludeWaitlist { get; set; }
}
