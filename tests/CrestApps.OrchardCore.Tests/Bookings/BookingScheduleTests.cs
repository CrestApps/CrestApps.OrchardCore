using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// A booking product's dates: which a booking takes, which are offered, what a unit costs for the people in it, and what
/// canceling costs.
/// </summary>
public sealed class BookingScheduleTests
{
    [Fact]
    public void AStay_TakesEveryNightFromCheckInToTheNightBeforeCheckOut()
    {
        Assert.Equal(["2026-12-30", "2026-12-31", "2027-01-01"], BookingSchedule.GetSlotKeys(BookingKind.Nightly, "2026-12-30", "2027-01-02"));
        Assert.Empty(BookingSchedule.GetSlotKeys(BookingKind.Nightly, "2026-12-30", "2026-12-30"));
        Assert.Equal(["2026-12-01"], BookingSchedule.GetSlotKeys(BookingKind.Departure, "2026-12-01", null));
        Assert.Equal(["2026-12-01T09:30"], BookingSchedule.GetSlotKeys(BookingKind.TimeSlot, "2026-12-01T09:30", null));
        Assert.Empty(BookingSchedule.GetSlotKeys(BookingKind.Departure, "1 December", null));
    }

    [Fact]
    public void TimeSlots_RunFromOpeningToClosing_EverySlotsLength()
    {
        var part = new BookingPart
        {
            Kind = BookingKind.TimeSlot,
            SlotMinutes = 45,
            Hours = [new BookingOpeningHours { Day = DayOfWeek.Tuesday, Opens = "09:00", Closes = "11:00" }],
        };

        var tuesday = new DateOnly(2026, 12, 1);

        Assert.Equal(
            [tuesday.ToDateTime(new TimeOnly(9, 0)), tuesday.ToDateTime(new TimeOnly(9, 45))],
            BookingSchedule.GetTimes(part, tuesday));
        Assert.Empty(BookingSchedule.GetTimes(part, tuesday.AddDays(1)));
    }

    [Fact]
    public void DatesAreChecked_AgainstTheProductsRules()
    {
        var now = new DateTime(2026, 11, 30, 10, 0, 0);
        var stay = new BookingPart { Kind = BookingKind.Nightly, MinNights = 2, MaxNights = 7, DaysAhead = 90 };
        var appointment = new BookingPart
        {
            Kind = BookingKind.TimeSlot,
            Hours = [new BookingOpeningHours { Day = DayOfWeek.Monday, Opens = "09:00", Closes = "17:00" }],
        };

        Assert.Null(BookingSchedule.Check(stay, "2026-12-01", "2026-12-03", now));
        Assert.Equal("nights", BookingSchedule.Check(stay, "2026-12-01", "2026-12-02", now));
        Assert.Equal("past", BookingSchedule.Check(stay, "2026-11-29", "2026-12-02", now));
        Assert.Equal("far", BookingSchedule.Check(stay, "2027-06-01", "2027-06-03", now));
        Assert.Equal("past", BookingSchedule.Check(appointment, "2026-11-30T09:00", null, now));
        Assert.Null(BookingSchedule.Check(appointment, "2026-11-30T11:00", null, now));
        Assert.Equal("closed", BookingSchedule.Check(appointment, "2026-11-30T11:30", null, now));
    }

    [Fact]
    public void AUnit_IsItsDatesPrices_ForEachPersonWhenPricedSo_PlusItsOption()
    {
        var tour = new BookingPart { Kind = BookingKind.Departure, PricePerPerson = true };
        var room = new BookingPart { Kind = BookingKind.Nightly };

        // A double room on a tour at 1,000 a person; a single at 1,000 with a 300 supplement.
        Assert.Equal(2000m, BookingSchedule.GetUnitPrice(tour, 2, [1000m], 0m));
        Assert.Equal(1300m, BookingSchedule.GetUnitPrice(tour, 1, [1000m], 300m));

        // Three nights of a room for two, one a weekend night.
        Assert.Equal(340m, BookingSchedule.GetUnitPrice(room, 2, [100m, 100m, 140m], 0m));
        Assert.Equal(0m, BookingSchedule.GetUnitPrice(room, 1, [50m], -80m));

        Assert.Equal(2, BookingSchedule.GetSeatsPerUnit(BookingKind.Departure, 2));
        Assert.Equal(1, BookingSchedule.GetSeatsPerUnit(BookingKind.Nightly, 2));
    }

    [Theory]
    [InlineData(90, 10)]
    [InlineData(60, 10)]
    [InlineData(45, 50)]
    [InlineData(30, 50)]
    [InlineData(10, 100)]
    public void Canceling_CostsTheTierItsStillWithin_AndEverythingCloserThanThem(int daysBefore, int feePercent)
    {
        BookingCancellationTier[] tiers =
        [
            new() { DaysBefore = 30, FeePercent = 50m },
            new() { DaysBefore = 60, FeePercent = 10m },
        ];

        Assert.Equal(feePercent, BookingSchedule.GetCancellationFeePercent(tiers, daysBefore));
    }

    [Fact]
    public void Canceling_WithNoPolicy_IsFree_AndTheRefundIsWhatWasPaidLessTheFee()
    {
        Assert.Equal(0m, BookingSchedule.GetCancellationFeePercent([], 1));

        // A 2,000 booking paid by a 15% deposit (300), canceled with a 10% fee (200): 100 back.
        Assert.Equal(100m, BookingSchedule.GetRefund(2000m, 300m, 10m));

        // A fee above what was paid refunds nothing; it never asks for more.
        Assert.Equal(0m, BookingSchedule.GetRefund(2000m, 300m, 50m));
        Assert.Equal(2000m, BookingSchedule.GetRefund(2000m, 2000m, 0m));
    }

    [Fact]
    public void TheBalance_IsDueItsDaysBeforeTheDate()
        => Assert.Equal(new DateOnly(2026, 10, 17), BookingSchedule.GetBalanceDueDate(new DateOnly(2026, 12, 1), 45));

    [Fact]
    public void ADateKey_StartsAtMidnightInTheSitesTimeZone()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

        Assert.Equal(new DateTime(2026, 11, 30, 22, 0, 0, DateTimeKind.Utc), BookingSchedule.GetStartUtc("2026-12-01", timeZone));
        Assert.Equal(new DateTime(2026, 12, 1, 7, 30, 0, DateTimeKind.Utc), BookingSchedule.GetStartUtc("2026-12-01T09:30", timeZone));
    }
}
