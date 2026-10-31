using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Reading and writing a worker's weekly availability as lines of text, the way the worker editor shows it.
/// </summary>
public sealed class AvailabilityFormatTests
{
    [Fact]
    public void Read_UnderstandsDaysTimesAndPreferences()
    {
        // Arrange
        var text = """
            Monday 09:00-17:00
            tuesday 08:00-12:00 preferred, 13:00-17:00
            Saturday unavailable
            Sunday 22:00-06:00
            """;
        var errors = new List<string>();

        // Act
        var windows = AvailabilityFormat.Read(text, errors);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(5, windows.Count);
        Assert.Contains(windows, window => window.Day == DayOfWeek.Monday && window.Start == new TimeOnly(9, 0) && window.End == new TimeOnly(17, 0) && window.Preference == AvailabilityPreference.Available);
        Assert.Contains(windows, window => window.Day == DayOfWeek.Tuesday && window.Start == new TimeOnly(8, 0) && window.Preference == AvailabilityPreference.Preferred);
        Assert.Contains(windows, window => window.Day == DayOfWeek.Tuesday && window.Start == new TimeOnly(13, 0) && window.Preference == AvailabilityPreference.Available);
        Assert.Contains(windows, window => window.Day == DayOfWeek.Saturday && window.Preference == AvailabilityPreference.Unavailable && window.Start == TimeOnly.MinValue && window.End == TimeOnly.MinValue);
        Assert.Contains(windows, window => window.Day == DayOfWeek.Sunday && window.Start == new TimeOnly(22, 0) && window.End == new TimeOnly(6, 0));
    }

    [Fact]
    public void Read_ReportsTheLinesItCannotRead()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var windows = AvailabilityFormat.Read("Funday 09:00-17:00\nMonday 9-17\nMonday 09:00-09:00", errors);

        // Assert
        Assert.Empty(windows);
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void Write_ThenRead_GivesTheSameWindows()
    {
        // Arrange
        IList<AvailabilityWindow> windows =
        [
            new AvailabilityWindow { Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0), End = new TimeOnly(12, 0), Preference = AvailabilityPreference.Preferred },
            new AvailabilityWindow { Day = DayOfWeek.Monday, Start = new TimeOnly(13, 0), End = new TimeOnly(17, 30) },
            new AvailabilityWindow { Day = DayOfWeek.Sunday, Preference = AvailabilityPreference.Unavailable },
        ];

        // Act
        var text = AvailabilityFormat.Write(windows);
        var read = AvailabilityFormat.Read(text, []);

        // Assert
        Assert.Equal(
            windows.Select(window => (window.Day, window.Start, window.End, window.Preference)),
            read.Select(window => (window.Day, window.Start, window.End, window.Preference)));
    }
}
