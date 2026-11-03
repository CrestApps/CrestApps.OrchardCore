namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Hears about appointment changes after they are committed, such as the workflow integration.
/// </summary>
public interface IAppointmentObserver
{
    /// <summary>
    /// Handles a committed change.
    /// </summary>
    /// <param name="change">What happened.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default);
}
