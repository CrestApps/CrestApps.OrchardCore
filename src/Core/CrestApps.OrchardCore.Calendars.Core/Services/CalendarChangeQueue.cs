using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Collects the event changes made in a scope and tells the <see cref="ICalendarEventObserver"/>s once the scope's session is
/// committed, through a deferred task in a new scope. Outside a shell scope (in unit tests) the changes are only collected.
/// </summary>
public sealed class CalendarChangeQueue
{
    private readonly List<CalendarEventChange> _pending = [];
    private bool _scheduled;

    /// <summary>
    /// Gets the changes collected and not yet handed to the observers.
    /// </summary>
    public IReadOnlyList<CalendarEventChange> Pending => _pending;

    /// <summary>
    /// Adds a change to tell the observers about once the session is committed.
    /// </summary>
    /// <param name="change">The change.</param>
    public void Enqueue(CalendarEventChange change)
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

            var observers = scope.ServiceProvider.GetServices<ICalendarEventObserver>().ToList();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<CalendarChangeQueue>>();

            foreach (var item in changes)
            {
                foreach (var observer in observers)
                {
                    try
                    {
                        await observer.OnChangedAsync(item);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "A calendar event observer ({Observer}) failed to handle a {Kind} change of the event '{EventId}'.", observer.GetType().Name, item.Kind, item.Event?.ItemId);
                    }
                }
            }
        });
    }
}
