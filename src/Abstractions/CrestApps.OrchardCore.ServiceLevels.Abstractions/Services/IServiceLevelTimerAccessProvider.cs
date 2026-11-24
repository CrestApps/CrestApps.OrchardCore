using System.Security.Claims;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Says which records of one kind a user may see the service-level timers of, for the ServiceLevelTimers data set. The
/// module that keeps the records registers one with
/// <see cref="ServiceLevelsServiceCollectionExtensions.AddServiceLevelTimerAccessProvider{TProvider}"/>; the timers of a
/// kind without one are shown only to those who manage service level policies.
/// </summary>
public interface IServiceLevelTimerAccessProvider
{
    /// <summary>
    /// Gets the kind of record, as kept in the timer's subject.
    /// </summary>
    string SubjectKind { get; }

    /// <summary>
    /// Gets the ids, out of the ones given, of the records the user may see.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="subjectIds">The record ids.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>The ids the user may see.</returns>
    Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, CancellationToken cancellationToken = default);
}
