using CrestApps.OrchardCore.Bookings.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Capacity per product and date, with holds: an unpaid order holds its dates, a paid one takes them, and a canceled or
/// expired one gives them back, the way stock is held, sold and released. A departure can only be booked on the dates
/// the store added; a night or a time slot on any date the product's rules offer.
/// </summary>
public sealed class BookingCapacityService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IBookingSlotStore _slots;
    private readonly IBookingStore _bookings;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IBookingSlotFilter[] _slotFilters;

    private TimeZoneInfo _timeZone;

    public BookingCapacityService(
        IBookingSlotStore slots,
        IBookingStore bookings,
        ISession session,
        IDistributedLock distributedLock,
        ISiteService siteService,
        IClock clock,
        ILogger<BookingCapacityService> logger,
        IEnumerable<IBookingSlotFilter> slotFilters = null)
    {
        _slots = slots;
        _bookings = bookings;
        _session = session;
        _distributedLock = distributedLock;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        _slotFilters = slotFilters?.ToArray() ?? [];
    }

    /// <summary>
    /// Returns the site's time zone, which dates are in.
    /// </summary>
    public async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        if (_timeZone is not null)
        {
            return _timeZone;
        }

        var site = await _siteService.GetSiteSettingsAsync();

        _timeZone = !string.IsNullOrEmpty(site?.TimeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(site.TimeZoneId, out var timeZone)
            ? timeZone
            : TimeZoneInfo.Utc;

        return _timeZone;
    }

    /// <summary>
    /// Returns the time now in the site's time zone.
    /// </summary>
    public async Task<DateTime> GetLocalNowAsync()
        => TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, await GetTimeZoneAsync());

    /// <summary>
    /// Returns the store's own dates for these keys, such as a departure's, with their prices and counts.
    /// </summary>
    public Task<IReadOnlyList<BookingSlot>> ListSlotsAsync(string contentItemId, IReadOnlyList<string> slotKeys, CancellationToken cancellationToken = default)
        => _slots.ListAsync(contentItemId, slotKeys, cancellationToken);

    /// <summary>
    /// Returns how much is left on the fullest of the dates, counting what the cart's own unpaid orders hold as its own.
    /// </summary>
    public async Task<int> GetRemainingAsync(string contentItemId, BookingPart part, IReadOnlyList<string> slotKeys, string cartId, CancellationToken cancellationToken = default)
    {
        if (slotKeys is not { Count: > 0 })
        {
            return 0;
        }

        var slots = await _slots.ListAsync(contentItemId, slotKeys, cancellationToken);
        var own = await _bookings.ListHeldByCartAsync(cartId, contentItemId, cancellationToken);
        var closed = await GetClosedKeysAsync(contentItemId, part, slotKeys, cancellationToken);
        var remaining = int.MaxValue;

        foreach (var key in slotKeys)
        {
            var slot = slots.FirstOrDefault(candidate => string.Equals(candidate.SlotKey, key, StringComparison.Ordinal));
            var left = closed.Contains(key) ? 0 : slot is null
                ? part.Kind == BookingKind.Departure ? 0 : Math.Max(0, part.Capacity)
                : slot.Closed ? 0 : slot.Remaining + own.Where(booking => booking.SlotKeys.Contains(key)).Sum(booking => booking.Seats);

            remaining = Math.Min(remaining, left);
        }

        return remaining == int.MaxValue ? 0 : remaining;
    }

    /// <summary>
    /// Holds a booking's dates, all or none. Returns whether it did.
    /// </summary>
    public async Task<bool> HoldAsync(Booking booking, BookingPart part, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ArgumentNullException.ThrowIfNull(part);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(GetLockKey(booking.ContentItemId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            if ((await GetClosedKeysAsync(booking.ContentItemId, part, booking.SlotKeys, cancellationToken)).Count > 0)
            {
                return false;
            }

            var slots = (await _slots.ListAsync(booking.ContentItemId, booking.SlotKeys, cancellationToken)).ToList();
            var timeZone = await GetTimeZoneAsync();
            var created = new List<BookingSlot>();

            foreach (var key in booking.SlotKeys)
            {
                var slot = slots.FirstOrDefault(candidate => string.Equals(candidate.SlotKey, key, StringComparison.Ordinal));

                if (slot is null)
                {
                    // A departure is only on the dates the store added; a night or a time slot is made when first held.
                    if (part.Kind == BookingKind.Departure || GetStartUtc(key, timeZone) is not { } startsUtc)
                    {
                        return false;
                    }

                    slot = new BookingSlot
                    {
                        ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                        ContentItemId = booking.ContentItemId,
                        SlotKey = key,
                        StartsUtc = startsUtc,
                        Capacity = Math.Max(0, part.Capacity),
                    };

                    created.Add(slot);
                    slots.Add(slot);
                }

                if (slot.Closed || slot.Remaining < booking.Seats)
                {
                    return false;
                }
            }

            var now = _clock.UtcNow;

            foreach (var key in booking.SlotKeys)
            {
                var slot = slots.First(candidate => string.Equals(candidate.SlotKey, key, StringComparison.Ordinal));

                slot.Held += booking.Seats;
                slot.ModifiedUtc = now;

                if (created.Contains(slot))
                {
                    await _slots.CreateAsync(slot, cancellationToken);
                }
                else
                {
                    await _slots.UpdateAsync(slot, cancellationToken);
                }
            }

            // Written before the lock is let go, so the next checkout sees what this one holds.
            await _session.FlushAsync(cancellationToken);
        }

        return true;
    }

    // The dates and times another feature closes, such as the Bookings Calendar feature closing what's busy on a calendar.
    private async Task<HashSet<string>> GetClosedKeysAsync(string contentItemId, BookingPart part, IReadOnlyList<string> slotKeys, CancellationToken cancellationToken)
    {
        var closed = new HashSet<string>(StringComparer.Ordinal);

        if (_slotFilters.Length == 0)
        {
            return closed;
        }

        var timeZone = await GetTimeZoneAsync();

        foreach (var filter in _slotFilters)
        {
            closed.UnionWith(await filter.GetClosedSlotKeysAsync(contentItemId, part, slotKeys, timeZone, cancellationToken));
        }

        return closed;
    }

    /// <summary>
    /// Turns a booking's hold into seats taken, once its order is paid.
    /// </summary>
    public Task CommitAsync(Booking booking, CancellationToken cancellationToken = default)
        => ChangeAsync(booking, slot =>
        {
            slot.Held = Math.Max(0, slot.Held - booking.Seats);
            slot.Sold += booking.Seats;
        }, cancellationToken);

    /// <summary>
    /// Gives a booking's dates back: what it held, or, with <paramref name="sold"/>, what it took.
    /// </summary>
    public Task ReleaseAsync(Booking booking, bool sold, CancellationToken cancellationToken = default)
        => ChangeAsync(booking, slot =>
        {
            if (sold)
            {
                slot.Sold = Math.Max(0, slot.Sold - booking.Seats);
            }
            else
            {
                slot.Held = Math.Max(0, slot.Held - booking.Seats);
            }
        }, cancellationToken);

    private async Task ChangeAsync(Booking booking, Action<BookingSlot> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(GetLockKey(booking.ContentItemId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new TimeoutException($"The dates of '{booking.ContentItemId}' are being changed elsewhere; try again.");
        }

        await using (locker)
        {
            var now = _clock.UtcNow;

            foreach (var slot in await _slots.ListAsync(booking.ContentItemId, booking.SlotKeys, cancellationToken))
            {
                change(slot);
                slot.ModifiedUtc = now;

                await _slots.UpdateAsync(slot, cancellationToken);
            }

            await _session.FlushAsync(cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("The dates of booking '{BookingId}' were changed.", booking.ItemId);
        }
    }

    internal static DateTime? GetStartUtc(string slotKey, TimeZoneInfo timeZone)
        => BookingSchedule.GetStartUtc(slotKey, timeZone);

    private static string GetLockKey(string contentItemId)
        => "BOOKING_SLOTS_" + contentItemId;
}
