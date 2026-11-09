using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Calendars;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// A start that fits a worker's day, with the trip to it.
/// </summary>
/// <param name="StartUtc">The start, in UTC.</param>
/// <param name="EndUtc">The end, in UTC.</param>
/// <param name="TravelMinutes">The trip from the stop before, in minutes.</param>
/// <param name="TravelKm">The trip from the stop before, in kilometers.</param>
/// <param name="TravelKnown">Whether the trip could be measured; it can't without both places.</param>
public sealed record SlotFit(DateTime StartUtc, DateTime EndUtc, int TravelMinutes, double TravelKm, bool TravelKnown = true);

/// <summary>
/// Why a time doesn't fit a worker's day.
/// </summary>
public enum SlotProblemKind
{
    /// <summary>
    /// They aren't on shift, or are on a break, then.
    /// </summary>
    OffShift,

    /// <summary>
    /// They are on time off then.
    /// </summary>
    TimeOff,

    /// <summary>
    /// Another visit overlaps it.
    /// </summary>
    Overlaps,

    /// <summary>
    /// They can't get there from the visit before in time.
    /// </summary>
    TooLateFromBefore,

    /// <summary>
    /// They can't get to the visit after in time.
    /// </summary>
    TooLateForAfter,
}

/// <summary>
/// Why a time doesn't fit, and the visit in the way.
/// </summary>
/// <param name="Kind">The kind of problem.</param>
/// <param name="Visit">The visit in the way, if any.</param>
public sealed record SlotProblem(SlotProblemKind Kind, ServiceVisit Visit = null);

/// <summary>
/// Fits a visit into a worker's day: on shift, off breaks and time off, clear of their other visits with the trips
/// between and a buffer.
/// </summary>
public sealed class SlotFitter
{
    private readonly DispatchResources _resources;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlotFitter"/> class.
    /// </summary>
    public SlotFitter(DispatchResources resources)
    {
        _resources = resources;
    }

    /// <summary>
    /// Finds the earliest start that fits.
    /// </summary>
    /// <param name="availability">The worker's day.</param>
    /// <param name="here">Where the visit is.</param>
    /// <param name="durationMinutes">How long it takes.</param>
    /// <param name="fromUtc">The earliest start, in UTC.</param>
    /// <param name="toUtc">The latest end, in UTC.</param>
    /// <param name="bufferMinutes">The minutes kept free next to other visits.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The start, or <see langword="null"/> when nothing fits.</returns>
    public async Task<SlotFit> FindEarliestAsync(WorkerAvailability availability, GeoPoint? here, int durationMinutes, DateTime fromUtc, DateTime toUtc, int bufferMinutes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(availability);

        var duration = TimeSpan.FromMinutes(durationMinutes);
        var buffer = TimeSpan.FromMinutes(bufferMinutes);

        foreach (var open in availability.Open.Where(open => open.EndUtc > fromUtc && open.StartUtc < toUtc))
        {
            var visits = availability.Visits.Where(visit => visit.StartUtc < open.EndUtc && visit.EndUtc > open.StartUtc).ToList();

            // The gaps: before the first visit, between each two, and after the last.
            for (var i = 0; i <= visits.Count; i++)
            {
                var before = i == 0 ? null : visits[i - 1];
                var after = i == visits.Count ? null : visits[i];
                var gapStart = before is null ? open.StartUtc : Max(open.StartUtc, before.EndUtc.Value + buffer);
                var gapEnd = after is null ? open.EndUtc : Min(open.EndUtc, after.StartUtc.Value - buffer);

                if (gapEnd - gapStart < duration)
                {
                    continue;
                }

                var from = before is null ? availability.Home : await _resources.PlaceOfAsync(before.WorkOrderId, cancellationToken);
                var tripIn = await _resources.TravelAsync(from, here, gapStart, cancellationToken);
                var start = RoundUp(Max(gapStart + (tripIn?.Duration ?? TimeSpan.Zero), fromUtc), 5);
                var end = start + duration;

                if (end > toUtc || end > gapEnd)
                {
                    continue;
                }

                if (after is not null)
                {
                    var tripOut = await _resources.TravelAsync(here, await _resources.PlaceOfAsync(after.WorkOrderId, cancellationToken), end, cancellationToken);

                    if (end + (tripOut?.Duration ?? TimeSpan.Zero) > gapEnd)
                    {
                        continue;
                    }
                }

                return new SlotFit(start, end, Minutes(tripIn), Kilometers(tripIn), tripIn is not null);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks that a time fits a worker's day.
    /// </summary>
    /// <param name="availability">The worker's day.</param>
    /// <param name="here">Where the visit is.</param>
    /// <param name="startUtc">The start, in UTC.</param>
    /// <param name="endUtc">The end, in UTC.</param>
    /// <param name="bufferMinutes">The minutes kept free next to other visits.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The fit with its trip, or the problem.</returns>
    public async Task<(SlotFit Fit, SlotProblem Problem)> CheckAsync(WorkerAvailability availability, GeoPoint? here, DateTime startUtc, DateTime endUtc, int bufferMinutes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(availability);

        var buffer = TimeSpan.FromMinutes(bufferMinutes);
        var overlapping = availability.Visits.FirstOrDefault(visit => visit.StartUtc < endUtc && visit.EndUtc > startUtc);

        if (overlapping is not null)
        {
            return (null, new SlotProblem(SlotProblemKind.Overlaps, overlapping));
        }

        if (availability.TimeOff.Any(span => span.Overlaps(new TimeWindow(startUtc, endUtc))))
        {
            return (null, new SlotProblem(SlotProblemKind.TimeOff));
        }

        if (!availability.Open.Any(open => open.StartUtc <= startUtc && open.EndUtc >= endUtc))
        {
            return (null, new SlotProblem(SlotProblemKind.OffShift));
        }

        var before = availability.Visits.LastOrDefault(visit => visit.EndUtc <= startUtc && visit.EndUtc > startUtc.AddHours(-12));
        var from = before is null ? availability.Home : await _resources.PlaceOfAsync(before.WorkOrderId, cancellationToken);
        var tripIn = await _resources.TravelAsync(from, here, before?.EndUtc ?? startUtc, cancellationToken);

        if (before is not null && before.EndUtc.Value + buffer + (tripIn?.Duration ?? TimeSpan.Zero) > startUtc)
        {
            return (null, new SlotProblem(SlotProblemKind.TooLateFromBefore, before));
        }

        var after = availability.Visits.FirstOrDefault(visit => visit.StartUtc >= endUtc && visit.StartUtc < endUtc.AddHours(12));

        if (after is not null)
        {
            var tripOut = await _resources.TravelAsync(here, await _resources.PlaceOfAsync(after.WorkOrderId, cancellationToken), endUtc, cancellationToken);

            if (endUtc + buffer + (tripOut?.Duration ?? TimeSpan.Zero) > after.StartUtc.Value)
            {
                return (null, new SlotProblem(SlotProblemKind.TooLateForAfter, after));
            }
        }

        return (new SlotFit(startUtc, endUtc, Minutes(tripIn), Kilometers(tripIn), tripIn is not null), null);
    }

    private static int Minutes(Geo.Models.TravelLeg? leg) => leg is null ? 0 : (int)Math.Ceiling(leg.Value.Duration.TotalMinutes);

    private static double Kilometers(Geo.Models.TravelLeg? leg) => leg is null ? 0 : Math.Round(leg.Value.DistanceMeters / 1000, 1);

    private static DateTime Max(DateTime first, DateTime second) => first > second ? first : second;

    private static DateTime Min(DateTime first, DateTime second) => first < second ? first : second;

    private static DateTime RoundUp(DateTime value, int minutes)
    {
        var step = TimeSpan.FromMinutes(minutes).Ticks;
        var ticks = (value.Ticks + step - 1) / step * step;

        return new DateTime(ticks, value.Kind);
    }
}
