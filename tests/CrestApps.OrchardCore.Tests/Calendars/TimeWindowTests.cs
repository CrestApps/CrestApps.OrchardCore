using CrestApps.OrchardCore.Calendars;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// <see cref="TimeWindow"/> is the one interval type every module uses for busy, free and open time.
/// </summary>
public sealed class TimeWindowTests
{
    private static readonly DateTime _monday = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Merge_JoinsWindowsThatOverlapOrTouch_AndSortsThem()
    {
        // Arrange
        TimeWindow[] windows = [At(14, 16), At(9, 11), At(10, 12), At(12, 13), At(18, 18)];

        // Act
        var merged = TimeWindow.Merge(windows);

        // Assert
        Assert.Equal([At(9, 13), At(14, 16)], merged);
    }

    [Fact]
    public void Subtract_TakesBreaksAndTimeOffOutOfAShift()
    {
        // Arrange
        var shift = At(8, 16);

        // Act
        var open = TimeWindow.Subtract([shift], [At(12, 13), At(15, 17), At(12, 12)]);

        // Assert
        Assert.Equal([At(8, 12), At(13, 15)], open);
    }

    [Fact]
    public void Subtract_AHoleThatCoversTheWindow_LeavesNothing()
    {
        Assert.Empty(TimeWindow.Subtract([At(9, 10)], [At(8, 11)]));
    }

    [Fact]
    public void Intersect_GivesTheSharedTime_OrNothing()
    {
        Assert.Equal(At(10, 11), At(9, 11).Intersect(At(10, 12)));
        Assert.Null(At(9, 10).Intersect(At(10, 11)));
    }

    [Fact]
    public void Overlaps_IsFalseForWindowsThatOnlyTouch()
    {
        Assert.True(At(9, 11).Overlaps(At(10, 12)));
        Assert.False(At(9, 10).Overlaps(At(10, 11)));
    }

    [Fact]
    public void Clip_CutsWindowsToARange()
    {
        Assert.Equal([At(9, 10), At(11, 12)], TimeWindow.Clip([At(8, 10), At(11, 13), At(14, 15)], At(9, 12)));
    }

    private static TimeWindow At(int startHour, int endHour)
        => new(_monday.AddHours(startHour), _monday.AddHours(endHour));
}
