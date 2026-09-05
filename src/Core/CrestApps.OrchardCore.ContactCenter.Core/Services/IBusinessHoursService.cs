namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Evaluates whether a business-hours calendar reports a queue as open at a given moment.
/// </summary>
public interface IBusinessHoursService
{
    /// <summary>
    /// Determines whether the specified calendar is currently open.
    /// </summary>
    /// <param name="calendarId">The calendar identifier; an empty value is always open.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the calendar is open or unrestricted; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsOpenAsync(string calendarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified calendar is open at the supplied UTC instant.
    /// </summary>
    /// <param name="calendarId">The calendar identifier; an empty value is always open.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the calendar is open or unrestricted; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified calendar is open at the supplied UTC instant using an optional time-zone override.
    /// </summary>
    /// <param name="calendarId">The calendar identifier; an empty value is always open.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <param name="timeZoneId">The time zone used instead of the calendar time zone when supplied.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the calendar is open or unrestricted; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsOpenAsync(
        string calendarId,
        DateTime utcInstant,
        string timeZoneId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a required calendar and distinguishes an unavailable or disabled calendar from an open calendar.
    /// </summary>
    /// <param name="calendarId">The required calendar identifier.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <param name="timeZoneId">The time zone used instead of the calendar time zone when supplied.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="null"/> when the calendar is unavailable or disabled; otherwise, whether it is open.</returns>
    Task<bool?> EvaluateAsync(
        string calendarId,
        DateTime utcInstant,
        string timeZoneId,
        CancellationToken cancellationToken = default);
}
