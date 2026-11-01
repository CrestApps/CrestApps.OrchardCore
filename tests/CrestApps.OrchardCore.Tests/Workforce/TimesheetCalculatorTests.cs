using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The timesheet calculator: punches paired into spans, rounding and grace, days split at midnight in the worker's time
/// (across daylight saving changes), daily and weekly overtime, meals, differentials, time off, premiums, and the
/// attendance exceptions it finds against the schedule.
/// </summary>
public sealed class TimesheetCalculatorTests
{
    private static readonly DateOnly _monday = new(2026, 11, 9);
    private static readonly PayCode _regular = Code("REG", PayCodeKind.Regular);
    private static readonly PayCode _overtime = Code("OT", PayCodeKind.Overtime, 1.5m);
    private static readonly PayCode _doubleTime = Code("DT", PayCodeKind.DoubleTime, 2);
    private static readonly PayCode _paidTimeOff = Code("PTO", PayCodeKind.PaidTimeOff);
    private static readonly PayCode _predictability = Code("PP", PayCodeKind.PredictabilityPay);

    private readonly DefaultTimesheetCalculator _calculator = new();

    [Fact]
    public void ADay_WithAMeal_PaysTheTimeBetweenTheMeal()
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, 9, 0),
            Punch(TimePunchKind.MealStart, _monday, 12, 0),
            Punch(TimePunchKind.MealEnd, _monday, 12, 30),
            Punch(TimePunchKind.Out, _monday, 17, 30));

        var result = _calculator.Calculate(input);

        Assert.Equal([(_monday, "REG", 8m)], Lines(result));
        Assert.Equal(480, result.WorkedMinutes);
        Assert.Empty(result.Exceptions);
    }

    [Fact]
    public void AnOvernightShift_SplitsAtMidnight()
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, 22, 0),
            Punch(TimePunchKind.Out, _monday.AddDays(1), 6, 0));

        var result = _calculator.Calculate(input);

        Assert.Equal([(_monday, "REG", 2m), (_monday.AddDays(1), "REG", 6m)], Lines(result));
    }

    [Fact]
    public void TheNightClocksGoBack_PaysTheRealHours()
    {
        // 1 November 2026 in New York has 25 hours: midnight to 08:00 is nine hours of work.
        var day = new DateOnly(2026, 11, 1);
        var input = Input(
            At(TimePunchKind.In, new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc)),
            At(TimePunchKind.Out, new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc)));

        input.TimeZoneId = "America/New_York";
        input.PeriodStart = day;
        input.PeriodEnd = day.AddDays(6);

        var result = _calculator.Calculate(input);

        Assert.Equal([(day, "REG", 9m)], Lines(result));
    }

    [Fact]
    public void TheNightClocksGoForward_PaysTheRealHours_AndSplitsAtTheRealMidnight()
    {
        // 8 March 2026 in New York has 23 hours. A shift from 20:00 on the 7th to 08:00 on the 8th is 11 hours:
        // four before midnight and seven after it.
        var day = new DateOnly(2026, 3, 7);
        var input = Input(
            At(TimePunchKind.In, new DateTime(2026, 3, 8, 1, 0, 0, DateTimeKind.Utc)),
            At(TimePunchKind.Out, new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc)));

        input.TimeZoneId = "America/New_York";
        input.PeriodStart = day;
        input.PeriodEnd = day.AddDays(6);

        var result = _calculator.Calculate(input);

        Assert.Equal([(day, "REG", 4m), (day.AddDays(1), "REG", 7m)], Lines(result));
    }

    [Fact]
    public void HoursPastFortyAWeek_AreOvertime_OnTheDayTheyHappen()
    {
        var punches = Enumerable.Range(0, 5)
            .SelectMany(offset => new[]
            {
                Punch(TimePunchKind.In, _monday.AddDays(offset), 8, 0),
                Punch(TimePunchKind.Out, _monday.AddDays(offset), 17, 0),
            })
            .ToArray();
        var input = Input(punches);

        var result = _calculator.Calculate(input);

        Assert.Equal(36m, Hours(result, "REG", _monday, _monday.AddDays(3)));
        Assert.Equal(4m, Hours(result, "REG", _monday.AddDays(4)));
        Assert.Equal(5m, Hours(result, "OT", _monday.AddDays(4)));
    }

    [Fact]
    public void ALongDay_IsRegular_ThenOvertime_ThenDoubleTime()
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, 6, 0),
            Punch(TimePunchKind.Out, _monday, 19, 0));

        input.Policy.DailyOvertimeHours = 8;
        input.Policy.DailyDoubleTimeHours = 12;

        var result = _calculator.Calculate(input);

        Assert.Equal([(_monday, "DT", 1m), (_monday, "OT", 4m), (_monday, "REG", 8m)], Lines(result));
    }

    [Fact]
    public void WeeklyOvertime_CountsTheDaysOfTheWeekBeforeThePeriod()
    {
        // A twice-a-month period starts on Monday the 16th, but its overtime week started on Sunday the 15th.
        var input = Input(
            Punch(TimePunchKind.In, new DateOnly(2026, 11, 15), 8, 0),
            Punch(TimePunchKind.Out, new DateOnly(2026, 11, 15), 23, 0),
            Punch(TimePunchKind.In, new DateOnly(2026, 11, 16), 0, 0),
            Punch(TimePunchKind.Out, new DateOnly(2026, 11, 16), 23, 0),
            Punch(TimePunchKind.In, new DateOnly(2026, 11, 17), 8, 0),
            Punch(TimePunchKind.Out, new DateOnly(2026, 11, 17), 12, 0));

        input.PeriodStart = new DateOnly(2026, 11, 16);
        input.PeriodEnd = new DateOnly(2026, 11, 30);

        var result = _calculator.Calculate(input);

        // 15 hours on the 15th are outside the period, but count: 23 on the 16th take the week to 38.
        Assert.Equal(
            [(new DateOnly(2026, 11, 16), "REG", 23m), (new DateOnly(2026, 11, 17), "OT", 2m), (new DateOnly(2026, 11, 17), "REG", 2m)],
            Lines(result));
    }

    [Theory]
    [InlineData(TimeRoundingDirection.FavorWorker, 9, 7, 17, 2, 8.25)]
    [InlineData(TimeRoundingDirection.Nearest, 9, 7, 17, 8, 8.25)]
    [InlineData(TimeRoundingDirection.Nearest, 9, 8, 17, 7, 7.75)]
    public void PunchTimes_RoundToTheStep(TimeRoundingDirection direction, int inHour, int inMinute, int outHour, int outMinute, double hours)
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, inHour, inMinute),
            Punch(TimePunchKind.Out, _monday, outHour, outMinute));

        input.Policy.RoundingMinutes = 15;
        input.Policy.RoundingDirection = direction;

        var result = _calculator.Calculate(input);

        Assert.Equal((decimal)hours, Hours(result, "REG", _monday));
    }

    [Fact]
    public void ALateStartInsideTheGrace_PaysFromTheShiftStart_AndOutsideIt_IsLate()
    {
        var shift = Shift("s1", _monday, 9, 17);
        var onTime = Input(Punch(TimePunchKind.In, _monday, 9, 4), Punch(TimePunchKind.Out, _monday, 17, 0));
        var late = Input(Punch(TimePunchKind.In, _monday, 9, 12), Punch(TimePunchKind.Out, _monday, 16, 50));

        onTime.Shifts = [shift];
        late.Shifts = [shift];

        var onTimeResult = _calculator.Calculate(onTime);
        var lateResult = _calculator.Calculate(late);

        Assert.Equal(8m, Hours(onTimeResult, "REG", _monday));
        Assert.Empty(onTimeResult.Exceptions);
        Assert.Equal(
            [(AttendanceExceptionKind.Late, 12), (AttendanceExceptionKind.EarlyOut, 10)],
            lateResult.Exceptions.Select(exception => (exception.Kind, exception.Minutes)).OrderBy(pair => pair.Kind));
        Assert.All(lateResult.Exceptions, exception => Assert.Equal("s1", exception.ShiftKey));
    }

    [Fact]
    public void AnEarlyStartInsideTheGrace_PaysFromTheShiftStart()
    {
        var input = Input(Punch(TimePunchKind.In, _monday, 8, 56), Punch(TimePunchKind.Out, _monday, 17, 3));

        input.Shifts = [Shift("s1", _monday, 9, 17)];

        var result = _calculator.Calculate(input);

        Assert.Equal(8m, Hours(result, "REG", _monday));
    }

    [Fact]
    public void AForgottenClockOut_IsAMissedPunch_AndPaysNothing_UnlessThePolicyClosesItAtTheShiftEnd()
    {
        var flagged = Input(Punch(TimePunchKind.In, _monday, 9, 0));
        var closed = Input(Punch(TimePunchKind.In, _monday, 9, 0));

        flagged.Shifts = [Shift("s1", _monday, 9, 17)];
        closed.Shifts = [Shift("s1", _monday, 9, 17)];
        closed.Policy.MissedPunchHandling = MissedPunchHandling.CloseAtScheduledEnd;

        var flaggedResult = _calculator.Calculate(flagged);
        var closedResult = _calculator.Calculate(closed);

        Assert.Empty(Lines(flaggedResult));
        Assert.Equal([AttendanceExceptionKind.MissedPunch], flaggedResult.Exceptions.Select(exception => exception.Kind));
        Assert.Equal([(_monday, "REG", 8m)], Lines(closedResult));
        Assert.Equal([AttendanceExceptionKind.MissedPunch], closedResult.Exceptions.Select(exception => exception.Kind));
        Assert.Null(closedResult.Spans.Single().EndPunchId);
    }

    [Fact]
    public void SomeoneStillOnTheClock_IsNotAMissedPunch()
    {
        var input = Input(Punch(TimePunchKind.In, _monday, 9, 0));

        input.NowUtc = Utc(_monday, 11, 0);

        var result = _calculator.Calculate(input);

        Assert.Empty(result.Lines);
        Assert.Empty(result.Exceptions);
        Assert.Equal(Utc(_monday, 9, 0), result.OnTheClockSinceUtc);
    }

    [Fact]
    public void AShiftWithNoPunches_IsANoShow_UnlessTheWorkerHadTimeOff()
    {
        var noShow = Input();
        var off = Input();

        noShow.Shifts = [Shift("s1", _monday, 9, 17)];
        off.Shifts = [Shift("s1", _monday, 9, 17)];
        off.TimeOff = [new TimesheetTimeOff { Date = _monday, Hours = 8 }];

        var noShowResult = _calculator.Calculate(noShow);
        var offResult = _calculator.Calculate(off);

        Assert.Equal([(AttendanceExceptionKind.NoShow, 480)], noShowResult.Exceptions.Select(exception => (exception.Kind, exception.Minutes)));
        Assert.Empty(offResult.Exceptions);
        Assert.Equal([(_monday, "PTO", 8m)], Lines(offResult));
    }

    [Fact]
    public void ALongDayWithNoMeal_HasTheMealTakenOff()
    {
        var input = Input(Punch(TimePunchKind.In, _monday, 9, 0), Punch(TimePunchKind.Out, _monday, 17, 0));

        input.Policy.AutoMealAfterHours = 6;
        input.Policy.AutoMealMinutes = 30;

        var result = _calculator.Calculate(input);

        Assert.Equal(7.5m, Hours(result, "REG", _monday));
    }

    [Fact]
    public void AMealLongerThanAllowed_IsALongMeal()
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, 9, 0),
            Punch(TimePunchKind.MealStart, _monday, 12, 0),
            Punch(TimePunchKind.MealEnd, _monday, 13, 15),
            Punch(TimePunchKind.Out, _monday, 17, 0));

        input.Policy.LongMealMinutes = 60;

        var result = _calculator.Calculate(input);

        Assert.Equal([(AttendanceExceptionKind.LongMeal, 15)], result.Exceptions.Select(exception => (exception.Kind, exception.Minutes)));
        Assert.Equal(6.75m, Hours(result, "REG", _monday));
    }

    [Fact]
    public void APaidBreak_CountsAsWork()
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, 9, 0),
            Punch(TimePunchKind.BreakStart, _monday, 10, 0),
            Punch(TimePunchKind.BreakEnd, _monday, 10, 15),
            Punch(TimePunchKind.Out, _monday, 13, 0));

        var result = _calculator.Calculate(input);

        Assert.Equal(4m, Hours(result, "REG", _monday));
        Assert.Equal([TimesheetSpanKind.Work, TimesheetSpanKind.Break, TimesheetSpanKind.Work], result.Spans.Select(span => span.Kind));
    }

    [Fact]
    public void ANightDifferential_AddsAPremiumLineForTheHoursInItsWindow()
    {
        var night = Code("NIGHT", PayCodeKind.ShiftDifferential);

        night.WindowStart = new TimeOnly(22, 0);
        night.WindowEnd = new TimeOnly(6, 0);
        night.PremiumPerHour = 2;

        var input = Input(Punch(TimePunchKind.In, _monday, 20, 0), Punch(TimePunchKind.Out, _monday.AddDays(1), 4, 0));

        input.PayCodes = [.. input.PayCodes, night];

        var result = _calculator.Calculate(input);

        Assert.Equal(2m, Hours(result, "NIGHT", _monday));
        Assert.Equal(4m, Hours(result, "NIGHT", _monday.AddDays(1)));
        Assert.Equal(8m, Hours(result, "REG", _monday, _monday.AddDays(1)));
    }

    [Fact]
    public void ActivityChanges_SplitTheLinesByActivity()
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, 9, 0, punch => punch.ActivityCodeId = "phones"),
            Punch(TimePunchKind.ActivityChange, _monday, 11, 0, punch => punch.ActivityCodeId = "training"),
            Punch(TimePunchKind.Out, _monday, 12, 0));

        var result = _calculator.Calculate(input);

        Assert.Equal(
            [("phones", 2m), ("training", 1m)],
            result.Lines.OrderBy(line => line.ActivityCodeId, StringComparer.Ordinal).Select(line => (line.ActivityCodeId, line.Hours)));
    }

    [Fact]
    public void PredictabilityPay_AndManualLines_AreAdded()
    {
        var input = Input();

        input.PredictabilityPay =
        [
            new PredictabilityPayEntry { ItemId = "p1", ShiftStartUtc = Utc(_monday.AddDays(2), 9, 0), Hours = 1, Status = PredictabilityPayStatus.Approved },
        ];
        input.ManualLines = [new TimesheetLine { Id = "m1", Date = _monday, PayCodeId = _regular.ItemId, Hours = 3, Source = TimesheetLineSource.Manual }];

        var result = _calculator.Calculate(input);

        Assert.Equal([(_monday, "REG", 3m), (_monday.AddDays(2), "PP", 1m)], Lines(result));
    }

    [Fact]
    public void APunchOutsideTheSite_IsAnException()
    {
        var input = Input(
            Punch(TimePunchKind.In, _monday, 9, 0, punch => punch.WithinGeofence = false),
            Punch(TimePunchKind.Out, _monday, 10, 0, punch => punch.WithinGeofence = true));

        var result = _calculator.Calculate(input);

        Assert.Equal([AttendanceExceptionKind.OutsideGeofence], result.Exceptions.Select(exception => exception.Kind));
        Assert.Equal("p1", result.Exceptions[0].PunchId);
    }

    [Fact]
    public void Exceptions_HaveKeysThatStayTheSame_WhenWorkedOutAgain()
    {
        var input = Input(Punch(TimePunchKind.In, _monday, 9, 30), Punch(TimePunchKind.Out, _monday, 17, 0));

        input.Shifts = [Shift("s1", _monday, 9, 17)];

        var first = _calculator.Calculate(input).Exceptions.Select(exception => exception.Key).ToArray();
        var second = _calculator.Calculate(input).Exceptions.Select(exception => exception.Key).ToArray();

        Assert.Single(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void PayPeriods_FollowTheirFrequency()
    {
        var weekly = new PayPeriodCalendar { Frequency = PayPeriodFrequency.Weekly, AnchorDate = new DateOnly(2026, 1, 4) };
        var biweekly = new PayPeriodCalendar { Frequency = PayPeriodFrequency.Biweekly, AnchorDate = new DateOnly(2026, 1, 4) };
        var semiMonthly = new PayPeriodCalendar { Frequency = PayPeriodFrequency.SemiMonthly };
        var monthly = new PayPeriodCalendar { Frequency = PayPeriodFrequency.Monthly };

        Assert.Equal((new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 14)), weekly.PeriodOn(new DateOnly(2026, 11, 10)));
        Assert.Equal((new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 21)), biweekly.PeriodOn(new DateOnly(2026, 11, 10)));
        Assert.Equal((new DateOnly(2025, 12, 21), new DateOnly(2026, 1, 3)), biweekly.PeriodOn(new DateOnly(2026, 1, 1)));
        Assert.Equal((new DateOnly(2026, 2, 16), new DateOnly(2026, 2, 28)), semiMonthly.PeriodOn(new DateOnly(2026, 2, 20)));
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15)), semiMonthly.PeriodOn(new DateOnly(2026, 2, 15)));
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), monthly.PeriodOn(new DateOnly(2026, 2, 20)));
        Assert.Equal(new DateOnly(2026, 11, 8), weekly.WeekOf(new DateOnly(2026, 11, 14)));
    }

    private static TimesheetCalculationInput Input(params TimePunch[] punches)
    {
        var number = 1;

        foreach (var punch in punches)
        {
            punch.ItemId ??= "p" + number++;
        }

        return new TimesheetCalculationInput
        {
            Policy = new TimeClockPolicy { EarlyGraceMinutes = 5, LateGraceMinutes = 5, WeeklyOvertimeHours = 40 },
            TimeZoneId = "UTC",
            PeriodStart = _monday.AddDays(-1),
            PeriodEnd = _monday.AddDays(12),
            WeekStart = DayOfWeek.Sunday,
            Punches = punches,
            PayCodes = [_regular, _overtime, _doubleTime, _paidTimeOff, _predictability],
            NowUtc = Utc(_monday.AddDays(13), 12, 0),
        };
    }

    private static PayCode Code(string code, PayCodeKind kind, decimal multiplier = 1)
    {
        return new PayCode { ItemId = code.ToLowerInvariant(), Code = code, Name = code, Kind = kind, Multiplier = multiplier };
    }

    private static TimePunch Punch(TimePunchKind kind, DateOnly date, int hour, int minute, Action<TimePunch> configure = null)
    {
        var punch = At(kind, Utc(date, hour, minute));

        configure?.Invoke(punch);

        return punch;
    }

    private static TimePunch At(TimePunchKind kind, DateTime utc)
    {
        return new TimePunch { WorkerId = "w", Kind = kind, Utc = utc, Source = TimePunchSource.Web };
    }

    private static TimesheetShift Shift(string key, DateOnly date, int startHour, int endHour)
    {
        return new TimesheetShift { Key = key, StartUtc = Utc(date, startHour, 0), EndUtc = Utc(date, endHour, 0) };
    }

    private static DateTime Utc(DateOnly date, int hour, int minute)
    {
        return date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc);
    }

    private static (DateOnly Date, string Code, decimal Hours)[] Lines(TimesheetCalculation result)
    {
        var codes = new[] { _regular, _overtime, _doubleTime, _paidTimeOff, _predictability }.ToDictionary(code => code.ItemId, code => code.Code);

        return result.Lines
            .GroupBy(line => (line.Date, Code: codes.GetValueOrDefault(line.PayCodeId, line.PayCodeId?.ToUpperInvariant())))
            .Select(group => (group.Key.Date, group.Key.Code, group.Sum(line => line.Hours)))
            .OrderBy(line => line.Date)
            .ThenBy(line => line.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static decimal Hours(TimesheetCalculation result, string code, DateOnly from, DateOnly? to = null)
    {
        return Lines(result).Where(line => line.Code == code && line.Date >= from && line.Date <= (to ?? from)).Sum(line => line.Hours);
    }
}
