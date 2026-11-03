using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Collects the appointment changes made in a scope and tells the <see cref="IAppointmentObserver"/>s once the session is
/// committed. Outside a shell scope (in unit tests) the changes are only collected.
/// </summary>
public sealed class AppointmentChangeQueue
{
    private readonly List<AppointmentChange> _pending = [];
    private bool _scheduled;

    /// <summary>
    /// Gets the changes not yet handed to the observers.
    /// </summary>
    public IReadOnlyList<AppointmentChange> Pending => _pending;

    /// <summary>
    /// Adds a change.
    /// </summary>
    /// <param name="change">The change.</param>
    public void Enqueue(AppointmentChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        _pending.Add(change);

        if (_scheduled || ShellScope.Current is null)
        {
            return;
        }

        _scheduled = true;

        ShellScope.AddDeferredTask(async scope =>
        {
            var changes = _pending.ToList();

            _pending.Clear();
            _scheduled = false;

            var observers = scope.ServiceProvider.GetServices<IAppointmentObserver>().ToList();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppointmentChangeQueue>>();

            foreach (var change in changes)
            {
                foreach (var observer in observers)
                {
                    try
                    {
                        await observer.OnChangedAsync(change);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An appointment observer ({Observer}) failed to handle a {Kind} change.", observer.GetType().Name, change.Kind);
                    }
                }
            }
        });
    }
}
