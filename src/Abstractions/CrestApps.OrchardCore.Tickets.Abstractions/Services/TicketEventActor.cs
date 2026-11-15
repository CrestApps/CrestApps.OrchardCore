using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Who made the change an <see cref="ITicketObserver"/> is being told about: an agent, a workflow or the site. The ticket
/// service sets it while it raises each event, so an observer (the audit trail, notifications) doesn't have to guess
/// from the request, which is the agent's even when a workflow made the change.
/// </summary>
public static class TicketEventActor
{
    private static readonly AsyncLocal<TicketActor> _current = new();

    /// <summary>
    /// Gets who made the change being raised, or <see langword="null"/> outside an event.
    /// </summary>
    public static TicketActor Current => _current.Value;

    /// <summary>
    /// Sets the actor until the returned scope is disposed.
    /// </summary>
    /// <param name="actor">The actor.</param>
    /// <returns>The scope that restores the previous actor.</returns>
    public static IDisposable Use(TicketActor actor)
    {
        var previous = _current.Value;
        _current.Value = actor;

        return new Restore(previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly TicketActor _previous;

        public Restore(TicketActor previous)
        {
            _previous = previous;
        }

        public void Dispose()
            => _current.Value = _previous;
    }
}
