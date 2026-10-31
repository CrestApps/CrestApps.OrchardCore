namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// The editor of the appointment workflow tasks. Each task shows the fields it has.
/// </summary>
public class AppointmentTaskViewModel
{
    /// <summary>
    /// Gets or sets the AppointmentId field.
    /// </summary>
    public string AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the AppointmentTypeId field.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the CustomerEmail field.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the CustomerName field.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the CustomerPhone field.
    /// </summary>
    public string CustomerPhone { get; set; }

    /// <summary>
    /// Gets or sets the From field.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the Notes field.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the Reason field.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the Start field.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets the To field.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes.
    /// </summary>
    public string OutputName { get; set; }
}
