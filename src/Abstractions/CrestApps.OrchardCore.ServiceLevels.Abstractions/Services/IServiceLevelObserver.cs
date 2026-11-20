using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Told what happens to service level timers. The module that owns a kind of record registers one with
/// <see cref="ServiceLevelsServiceCollectionExtensions.AddServiceLevelObserver{TObserver}"/> and reacts to the timers of
/// its own records (<c>timer.Subject.Kind</c>): it raises its workflow events, notifies people, escalates.
/// </summary>
public interface IServiceLevelObserver
{
    /// <summary>
    /// A timer reached its warning point and its work isn't done.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task WarningAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// A timer passed its target and its work isn't done.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task BreachedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// The work a timer measured was done, within the target or not (<see cref="ServiceLevelTimer.IsBreached"/>).
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task CompletedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// A running timer's business hours schedule was changed so that it is never open, so the timer now counts around
    /// the clock (<see cref="ServiceLevelTimer.NoBusinessHours"/>). Told once, when it happens.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task NoBusinessHoursAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
