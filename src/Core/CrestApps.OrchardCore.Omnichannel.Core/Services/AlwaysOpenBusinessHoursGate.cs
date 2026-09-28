namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The default <see cref="IBusinessHoursGate"/> for a tenant with no business-hours calendars. Every moment is
/// open, because a tenant that has not defined closing times has not asked for any send to be held back; gating
/// on a calendar that does not exist would stop every send rather than the after-hours ones.
/// </summary>
public sealed class AlwaysOpenBusinessHoursGate : IBusinessHoursGate
{
    /// <inheritdoc/>
    public Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, string timeZoneId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<IReadOnlyList<BusinessHoursCalendarOption>> GetCalendarOptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BusinessHoursCalendarOption>>([]);
}
