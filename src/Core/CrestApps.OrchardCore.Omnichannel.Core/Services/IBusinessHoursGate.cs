namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Abstracts the business-hours check used to gate background-initiated sends (such as re-engagement nudges) without
/// coupling the Omnichannel automation to the module that owns business-hours calendars. A feature that provides
/// calendars (for example ContactCenter) registers an implementation; when none is registered the automation treats
/// every moment as open, so the gate degrades gracefully.
/// </summary>
public interface IBusinessHoursGate
{
    /// <summary>
    /// Determines whether the given calendar is open at the supplied UTC instant, evaluated in the supplied time zone.
    /// </summary>
    /// <param name="calendarId">The calendar identifier; an empty value is treated as always open.</param>
    /// <param name="utcInstant">The UTC instant to evaluate.</param>
    /// <param name="timeZoneId">The time zone to evaluate in (typically the contact's local time zone); optional.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the calendar is open or unrestricted; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, string timeZoneId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the available business-hours calendars as (id, name) pairs, for presenting a picker in an editor.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<BusinessHoursCalendarOption>> GetCalendarOptionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A selectable business-hours calendar, exposed for editor pickers without leaking the owning module's model type.
/// </summary>
/// <param name="Id">The calendar identifier.</param>
/// <param name="Name">The calendar display name.</param>
public sealed record BusinessHoursCalendarOption(string Id, string Name);
