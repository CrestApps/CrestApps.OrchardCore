using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Bookings.Calendar;
using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// The booking store tells its handlers what changed, and the Bookings Calendar feature keeps a calendar event for each
/// booking on a real SQLite store.
/// </summary>
public sealed class BookingCalendarTests
{
    private static readonly DateTime _startsUtc = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Store_TellsHandlersWhatChanged()
    {
        // Arrange
        var recorder = new RecordingBookingHandler();
        await using var harness = await CreateAsync(services => services.AddSingleton<IBookingHandler>(recorder));

        // Act
        var booking = await SaveAsync(harness, null, b => b.Status = BookingStatus.Held);
        await SaveAsync(harness, booking.ItemId, b => b.Status = BookingStatus.Confirmed);
        await SaveAsync(harness, booking.ItemId, b => b.StartsUtc = b.StartsUtc.AddDays(1));

        // Assert
        Assert.Collection(
            recorder.Saved,
            created =>
            {
                Assert.True(created.IsNew);
                Assert.Null(created.PreviousStatus);
                Assert.False(created.StatusChanged);
            },
            confirmed =>
            {
                Assert.False(confirmed.IsNew);
                Assert.Equal(BookingStatus.Held, confirmed.PreviousStatus);
                Assert.True(confirmed.StatusChanged);
                Assert.False(confirmed.Rescheduled);
            },
            moved =>
            {
                Assert.False(moved.IsNew);
                Assert.False(moved.StatusChanged);
                Assert.True(moved.Rescheduled);
            });
    }

    [Fact]
    public async Task Calendar_FollowsTheBooking()
    {
        // Arrange
        await using var harness = await CreateAsync(AddBookingsCalendar);

        // Act: held, then confirmed and moved
        var booking = await SaveAsync(harness, null, b => b.Status = BookingStatus.Held);
        var held = await FindEventAsync(harness, booking.ItemId);

        await SaveAsync(harness, booking.ItemId, b =>
        {
            b.Status = BookingStatus.Confirmed;
            b.StartsUtc = _startsUtc.AddDays(1);
            b.EndsUtc = _startsUtc.AddDays(1).AddHours(2);
        });
        var confirmed = await FindEventAsync(harness, booking.ItemId);

        // Assert
        Assert.NotNull(held);
        Assert.Equal(CalendarEventStatus.Tentative, held.Status);
        Assert.False(held.Editable);
        Assert.Equal(_startsUtc, held.StartUtc);
        Assert.Equal(_startsUtc.AddHours(1), held.EndUtc);
        Assert.Equal("City tour: Ada Lovelace", held.Title);
        Assert.Contains(held.Attendees, attendee => attendee.Email == "ada@example.com");

        Assert.Equal(held.ItemId, confirmed.ItemId);
        Assert.Equal(CalendarEventStatus.Confirmed, confirmed.Status);
        Assert.Equal(_startsUtc.AddDays(1), confirmed.StartUtc);
        Assert.Equal(_startsUtc.AddDays(1).AddHours(2), confirmed.EndUtc);

        var calendar = await harness.RunAsync(services => services.GetRequiredService<ICalendarService>().FindByIdAsync(confirmed.CalendarId).AsTask(), commit: false);

        Assert.Equal(BookingCalendarHandler.CalendarType, calendar.Type);
        Assert.Equal(BookingCalendarHandler.Source, calendar.Source);
    }

    [Fact]
    public async Task Calendar_CancelsAndDeletesWithTheBooking()
    {
        // Arrange
        await using var harness = await CreateAsync(AddBookingsCalendar);
        var canceled = await SaveAsync(harness, null, b => b.Status = BookingStatus.Confirmed);
        var deleted = await SaveAsync(harness, null, b => b.Status = BookingStatus.Confirmed);

        // Act
        await SaveAsync(harness, canceled.ItemId, b => b.Status = BookingStatus.Canceled);
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<IBookingStore>();

            await store.DeleteAsync(await store.FindByIdAsync(deleted.ItemId));
        });

        // Assert
        Assert.Equal(CalendarEventStatus.Cancelled, (await FindEventAsync(harness, canceled.ItemId)).Status);
        Assert.Null(await FindEventAsync(harness, deleted.ItemId));
    }

    [Fact]
    public async Task Calendar_SkipsBookingsThatNeverBooked()
    {
        // Arrange
        await using var harness = await CreateAsync(AddBookingsCalendar);

        // Act
        var expired = await SaveAsync(harness, null, b => b.Status = BookingStatus.Expired);

        // Assert
        Assert.Null(await FindEventAsync(harness, expired.ItemId));
    }

    [Fact]
    public async Task SlotFilter_ClosesTimesBusyOnTheBlockingCalendar()
    {
        // Arrange: the room is busy 10:00-11:00 New York time on 10 November
        await using var harness = await CreateAsync(AddBookingsCalendar);
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Board Room");

        await harness.CreateEventAsync(room.ItemId, new DateTime(2026, 11, 10, 10, 0, 0), new DateTime(2026, 11, 10, 11, 0, 0));

        var part = new BookingPart { Kind = BookingKind.TimeSlot, SlotMinutes = 30, BlockingCalendarId = room.ItemId };
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        string[] keys = ["2026-11-10T09:30", "2026-11-10T10:00", "2026-11-10T10:30", "2026-11-10T11:00", "2026-11-11"];

        // Act
        var closed = await harness.RunAsync(
            services => services.GetRequiredService<BookingCalendarSlotFilter>().GetClosedSlotKeysAsync("tour1", part, keys, timeZone, TestContext.Current.CancellationToken),
            commit: false);
        var unblocked = await harness.RunAsync(
            services => services.GetRequiredService<BookingCalendarSlotFilter>().GetClosedSlotKeysAsync("tour1", new BookingPart { Kind = BookingKind.TimeSlot }, keys, timeZone, TestContext.Current.CancellationToken),
            commit: false);

        // Assert
        Assert.Equal(["2026-11-10T10:00", "2026-11-10T10:30"], closed.Order(StringComparer.Ordinal));
        Assert.Empty(unblocked);
    }

    private static void AddBookingsCalendar(IServiceCollection services)
    {
        services.AddScoped<BookingCalendarHandler>();
        services.AddScoped<IBookingHandler>(serviceProvider => serviceProvider.GetRequiredService<BookingCalendarHandler>());
        services.AddScoped<BookingCalendarSlotFilter>();
        services.AddCalendarType(BookingCalendarHandler.CalendarType, type => type
            .ManagedByCode()
            .AddEventType(BookingCalendarHandler.EventType, new LocalizationSource("Booking")));
    }

    private static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure)
    {
        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddScoped<IBookingStore, BookingStore>();
                configure(services);
            },
            createSchema: schemaBuilder => schemaBuilder.CreateMapIndexTableAsync<BookingIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("OrderId", column => column.WithLength(26))
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("CartId", column => column.Nullable().WithLength(26))
                .Column<string>("Title", column => column.Nullable().WithLength(255))
                .Column<string>("CustomerName", column => column.Nullable().WithLength(255))
                .Column<DateTime>("StartsUtc")
                .Column<int>("Status")
                .Column<DateTime>("HoldExpiresUtc", column => column.Nullable())
                .Column<DateTime>("CreatedUtc"),
                collection: BookingsConstants.CollectionName),
            indexProviders: [new BookingIndexProvider()],
            collections: [BookingsConstants.CollectionName]);
    }

    private static Task<Booking> SaveAsync(CalendarTestHarness harness, string bookingId, Action<Booking> change)
    {
        return harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<IBookingStore>();

            if (bookingId is null)
            {
                var booking = new Booking
                {
                    OrderId = "order1",
                    OrderNumber = "1001",
                    ContentItemId = "tour1",
                    Title = "City tour",
                    StartsUtc = _startsUtc,
                    CustomerName = "Ada Lovelace",
                    CustomerEmail = "ada@example.com",
                    CreatedUtc = CalendarTestHarness.Now,
                    ModifiedUtc = CalendarTestHarness.Now,
                };

                change(booking);
                await store.CreateAsync(booking);

                return booking;
            }

            var existing = await store.FindByIdAsync(bookingId);

            change(existing);
            existing.ModifiedUtc = CalendarTestHarness.Now.AddMinutes(5);
            await store.UpdateAsync(existing);

            return existing;
        });
    }

    private static Task<CalendarEvent> FindEventAsync(CalendarTestHarness harness, string bookingId)
    {
        return harness.RunAsync(
            async services =>
            {
                var events = await services.GetRequiredService<ICalendarEventManager>().FindBySourceAsync(BookingCalendarHandler.Source, bookingId);

                return events.Count > 0 ? events[0] : null;
            },
            commit: false);
    }

    private sealed class RecordingBookingHandler : IBookingHandler
    {
        public List<BookingSavingContext> Saved { get; } = [];

        public Task SavingAsync(BookingSavingContext context, CancellationToken cancellationToken = default)
        {
            // The booking is the same instance across saves, so its values are copied.
            Saved.Add(new BookingSavingContext
            {
                Booking = new Booking { Status = context.Booking.Status, StartsUtc = context.Booking.StartsUtc, EndsUtc = context.Booking.EndsUtc },
                IsNew = context.IsNew,
                PreviousStatus = context.PreviousStatus,
                PreviousStartsUtc = context.PreviousStartsUtc,
                PreviousEndsUtc = context.PreviousEndsUtc,
            });

            return Task.CompletedTask;
        }

        public Task DeletingAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
