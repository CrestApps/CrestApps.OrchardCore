using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The periods and pay dates of a pay schedule.
/// </summary>
public sealed class PayScheduleTests
{
    [Fact]
    public void Biweekly_CountsPeriodsFromTheAnchor_BackwardAndForward()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.Biweekly, AnchorDate = new DateOnly(2026, 1, 4) };

        Assert.Equal((new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 17)), schedule.PeriodOn(new DateOnly(2026, 1, 10)));
        Assert.Equal((new DateOnly(2025, 12, 21), new DateOnly(2026, 1, 3)), schedule.PeriodOn(new DateOnly(2026, 1, 3)));
    }

    [Fact]
    public void FourWeekly_HasTwentyEightDays()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.FourWeekly, AnchorDate = new DateOnly(2026, 1, 4) };

        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), schedule.PeriodOn(new DateOnly(2026, 2, 15)));
    }

    [Fact]
    public void SemiMonthly_UsesTheChosenSecondPeriodStart()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.SemiMonthly, SecondPeriodStartDay = 11 };

        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 10)), schedule.PeriodOn(new DateOnly(2026, 2, 10)));
        Assert.Equal((new DateOnly(2026, 2, 11), new DateOnly(2026, 2, 28)), schedule.PeriodOn(new DateOnly(2026, 2, 11)));
    }

    [Fact]
    public void SemiMonthly_ByDefault_SplitsOnTheSixteenth_AsTheTimeClockDid()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.SemiMonthly };

        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15)), schedule.PeriodOn(new DateOnly(2026, 3, 15)));
        Assert.Equal((new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 31)), schedule.PeriodOn(new DateOnly(2026, 3, 16)));
    }

    [Fact]
    public void Quarterly_StartsInTheAnchorsMonth()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.Quarterly, AnchorDate = new DateOnly(2026, 2, 1) };

        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 4, 30)), schedule.PeriodOn(new DateOnly(2026, 4, 30)));
        Assert.Equal((new DateOnly(2025, 11, 1), new DateOnly(2026, 1, 31)), schedule.PeriodOn(new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void Yearly_RunsTwelveMonthsFromTheAnchorsMonth()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.Yearly, AnchorDate = new DateOnly(2026, 7, 1) };

        Assert.Equal((new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30)), schedule.PeriodOn(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void PayDate_DaysAfterThePeriod_MovesOffAWeekendToTheFridayBefore()
    {
        // The period ends on a Saturday; five days later is a Thursday, two days later a Monday, one day a Sunday.
        var schedule = new PaySchedule { PayDateRule = PayDateRule.DaysAfterPeriodEnd, PayDateOffsetDays = 1, NonBusinessDayShift = PayDateShift.Before };

        Assert.Equal(new DateOnly(2026, 1, 16), schedule.PayDateFor(new DateOnly(2026, 1, 17)));
    }

    [Fact]
    public void PayDate_CanMoveToTheBusinessDayAfter_OrStay()
    {
        var after = new PaySchedule { PayDateOffsetDays = 0, NonBusinessDayShift = PayDateShift.After };
        var none = new PaySchedule { PayDateOffsetDays = 0, NonBusinessDayShift = PayDateShift.None };

        Assert.Equal(new DateOnly(2026, 1, 19), after.PayDateFor(new DateOnly(2026, 1, 17)));
        Assert.Equal(new DateOnly(2026, 1, 17), none.PayDateFor(new DateOnly(2026, 1, 17)));
    }

    [Fact]
    public void PayDate_SkipsHolidaysTheCallerKnows()
    {
        var schedule = new PaySchedule { PayDateOffsetDays = 3, NonBusinessDayShift = PayDateShift.Before };
        var holiday = new DateOnly(2026, 1, 20);

        // The period ends Saturday the 17th; Tuesday the 20th is a holiday, so pay moves to Monday the 19th.
        Assert.Equal(new DateOnly(2026, 1, 19), schedule.PayDateFor(new DateOnly(2026, 1, 17), day => day != holiday && day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)));
    }

    [Fact]
    public void PayDate_FixedDays_PaysEachHalfOnItsDay_AndZeroIsTheLastDay()
    {
        var schedule = new PaySchedule
        {
            Frequency = PayFrequency.SemiMonthly,
            PayDateRule = PayDateRule.FixedDays,
            FirstPayDay = 15,
            SecondPayDay = 0,
            NonBusinessDayShift = PayDateShift.None,
        };

        Assert.Equal(new DateOnly(2026, 4, 15), schedule.PayDateFor(new DateOnly(2026, 4, 15)));
        Assert.Equal(new DateOnly(2026, 4, 30), schedule.PayDateFor(new DateOnly(2026, 4, 30)));
    }

    [Fact]
    public void PayDate_FixedDays_AreIgnoredForAWeeklySchedule()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.Weekly, PayDateRule = PayDateRule.FixedDays, PayDateOffsetDays = 4, NonBusinessDayShift = PayDateShift.None };

        Assert.Equal(new DateOnly(2026, 1, 21), schedule.PayDateFor(new DateOnly(2026, 1, 17)));
    }

    [Fact]
    public void WeekOf_StartsOnTheOvertimeWeekDay()
    {
        var schedule = new PaySchedule { WeekStart = DayOfWeek.Monday };

        Assert.Equal(new DateOnly(2026, 1, 12), schedule.WeekOf(new DateOnly(2026, 1, 18)));
    }
}
