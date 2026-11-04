using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The text formats of the shift planning editors: the breaks and activities inside a shift, and the rows of a
/// coverage requirement.
/// </summary>
public sealed class PlanningFormatTests
{
    private static readonly ActivityCode[] _codes =
    [
        new ActivityCode { ItemId = "meal", Name = "Meal", Code = "MEAL" },
        new ActivityCode { ItemId = "break", Name = "Break", Code = "BRK" },
        new ActivityCode { ItemId = "training", Name = "Team training" },
    ];

    [Fact]
    public void Segments_ReadByNameOrCode_AndWriteBackByName()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var segments = SegmentFormat.Read("13:00-13:30 meal\n10:15-10:30 BRK\n\n15:00-16:00 Team training", _codes, errors);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(["meal", "break", "training"], segments.Select(segment => segment.ActivityCodeId));
        Assert.Equal(new TimeOnly(10, 15), segments[1].Start);
        Assert.Equal(
            "10:15-10:30 Break" + Environment.NewLine + "13:00-13:30 Meal" + Environment.NewLine + "15:00-16:00 Team training",
            SegmentFormat.Write(segments, _codes, new TimeOnly(9, 0)));
    }

    [Fact]
    public void Segments_RejectAnUnknownActivity_OrABadRange()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var segments = SegmentFormat.Read("13:00-13:30 Nap\n13:00 Meal\n14:00-13:00 Meal", _codes, errors);

        // Assert
        Assert.Empty(segments);
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void Segments_OfANightShift_BecomeOffsetsPastMidnight()
    {
        // Arrange
        var errors = new List<string>();
        var segments = SegmentFormat.Read("02:00-02:30 Meal", _codes, errors);

        // Act
        var template = SegmentFormat.ToTemplate(segments, new TimeOnly(22, 0));
        var draft = SegmentFormat.ToDraft(segments, new DateTime(2026, 11, 9, 22, 0, 0));

        // Assert
        Assert.Equal((240, 30), (template[0].OffsetMinutes, template[0].LengthMinutes));
        Assert.Equal(new DateTime(2026, 11, 10, 2, 0, 0), draft[0].Start);
        Assert.Equal(new DateTime(2026, 11, 10, 2, 30, 0), draft[0].End);
    }

    [Fact]
    public void CoverageRows_ReadWeekdaysAndDates_WithAnOptionalMaximum()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var rows = CoverageRowFormat.Read("Monday 09:00-17:00 1 2\nSat 10:00-14:00 1 1 3\n2026-12-24 08:00-12:00 0 1\nFriday 17:00-24:00 2 2", errors);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(4, rows.Count);
        Assert.Equal((DayOfWeek.Monday, 1, 2, (int?)null), (rows[0].Day.Value, rows[0].Minimum, rows[0].Target, rows[0].Maximum));
        Assert.Equal((DayOfWeek.Saturday, 3), (rows[1].Day.Value, rows[1].Maximum.Value));
        Assert.Equal(new DateOnly(2026, 12, 24), rows[2].Date);
        Assert.Null(rows[2].Day);
        Assert.Equal(TimeOnly.MinValue, rows[3].End);
    }

    [Fact]
    public void CoverageRows_WriteBackWhatTheyRead()
    {
        // Arrange
        var errors = new List<string>();
        const string text = "Monday 09:00-17:00 1 2\nFriday 17:00-24:00 2 2 4\n2026-12-24 08:00-12:00 0 1";

        // Act
        var written = CoverageRowFormat.Write(CoverageRowFormat.Read(text, errors));

        // Assert
        Assert.Empty(errors);
        Assert.Equal(text.Replace("\n", Environment.NewLine), written);
    }

    [Fact]
    public void CoverageRows_RejectMissingCounts_AndAnUnknownDay()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var rows = CoverageRowFormat.Read("Monday 09:00-17:00\nSomeday 09:00-17:00 1 2\nMonday 9-5 1 2", errors);

        // Assert
        Assert.Empty(rows);
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void ShiftTimes_EndingAtOrBeforeTheStart_EndTheNextDay()
    {
        // Act
        var (start, end) = PlanningInput.OnDay(new DateOnly(2026, 11, 9), new TimeOnly(22, 0), new TimeOnly(6, 0));

        // Assert
        Assert.Equal(new DateTime(2026, 11, 9, 22, 0, 0), start);
        Assert.Equal(new DateTime(2026, 11, 10, 6, 0, 0), end);
        Assert.Equal(480, PlanningInput.LengthMinutes(new TimeOnly(9, 0), new TimeOnly(17, 0)));
        Assert.Equal(24 * 60, PlanningInput.LengthMinutes(new TimeOnly(7, 0), new TimeOnly(7, 0)));
        Assert.True(PlanningInput.TryParseTime("7:30", out var time));
        Assert.Equal(new TimeOnly(7, 30), time);
        Assert.False(PlanningInput.TryParseTime("7.30", out _));
    }
}
