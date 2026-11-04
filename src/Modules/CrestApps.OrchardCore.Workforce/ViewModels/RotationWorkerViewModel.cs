namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A worker who follows a rotation.
/// </summary>
public class RotationWorkerViewModel
{
    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the day the worker started week 1.
    /// </summary>
    public DateOnly StartDate { get; set; }
}
