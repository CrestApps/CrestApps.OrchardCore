using CrestApps.OrchardCore.BusinessHours.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IBusinessHoursService"/> over the shared
/// <see cref="IOpenHoursService"/>, so a queue, entry point or dialer profile reads its Business Hours schedule the way
/// every other module reads business hours. A calendar id is the id of a Business Hours schedule.
/// </summary>
public sealed class DefaultBusinessHoursService : IBusinessHoursService
{
    private readonly IOpenHoursService _openHoursService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBusinessHoursService"/> class.
    /// </summary>
    /// <param name="openHoursService">The shared open-hours service.</param>
    /// <param name="clock">The clock used to resolve the current instant.</param>
    public DefaultBusinessHoursService(
        IOpenHoursService openHoursService,
        IClock clock)
    {
        _openHoursService = openHoursService;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<bool> IsOpenAsync(string calendarId, CancellationToken cancellationToken = default)
    {
        return IsOpenAsync(calendarId, _clock.UtcNow, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, CancellationToken cancellationToken = default)
    {
        return await IsOpenAsync(calendarId, utcInstant, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsOpenAsync(
        string calendarId,
        DateTime utcInstant,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        return await EvaluateAsync(calendarId, utcInstant, timeZoneId, cancellationToken) ?? true;
    }

    /// <inheritdoc/>
    public Task<bool?> EvaluateAsync(
        string calendarId,
        DateTime utcInstant,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        return _openHoursService.IsOpenAsync(calendarId, utcInstant, timeZoneId, cancellationToken);
    }
}
