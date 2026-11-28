using System.Security.Claims;
using CrestApps.OrchardCore.TimeTracker.Models;

namespace CrestApps.OrchardCore.TimeTracker.Services;

/// <summary>
/// Lets a person punch on a clock that keeps their time (an <see cref="IExternalTimeClock"/>) from somewhere other than
/// the clock's own page, such as the AI assistant: the clock checks the person and its own rules, as on its page.
/// </summary>
public interface IExternalTimeClockPunches
{
    /// <summary>
    /// Gets the id of the clock, as <see cref="ExternalTimeClockInfo.Id"/> gives it.
    /// </summary>
    string ClockId { get; }

    /// <summary>
    /// Gets where the person stands on the clock and the punches they can make next.
    /// </summary>
    /// <param name="user">The signed-in person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state; <see cref="ExternalTimeClockPunchState.Refusal"/> says why the person can't punch here.</returns>
    Task<ExternalTimeClockPunchState> GetStateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a punch for the person, now.
    /// </summary>
    /// <param name="user">The signed-in person.</param>
    /// <param name="punch">The punch, one of <see cref="ExternalTimeClockPunchState.NextPunches"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The punch made, or why it was refused.</returns>
    Task<ExternalTimeClockPunchResult> PunchAsync(ClaimsPrincipal user, string punch, CancellationToken cancellationToken = default);
}
