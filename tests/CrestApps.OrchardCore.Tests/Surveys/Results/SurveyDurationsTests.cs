using CrestApps.OrchardCore.Surveys.Core.Results;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// R11-L5: the staff pages show durations from their totals, so a page left open for 75 minutes is not 15:00 and a
/// response completed 50 hours after it started is not 2:00:00.
/// </summary>
public sealed class SurveyDurationsTests
{
    [Theory]
    [InlineData(50 * 3600, true, "50:00:00")]
    [InlineData(50 * 3600 + 61, false, "50:01")]
    [InlineData(3725, true, "1:02:05")]
    [InlineData(-5, true, "0:00:00")]
    public void Hours_CountsWholeDaysAsHours(int seconds, bool withSeconds, string expected)
    {
        // Act
        var text = SurveyDurations.Hours(TimeSpan.FromSeconds(seconds), withSeconds);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(75 * 60, "75:00")]
    [InlineData(65, "1:05")]
    [InlineData(-1, "0:00")]
    public void Minutes_CountsWholeHoursAsMinutes(int seconds, string expected)
    {
        // Act
        var text = SurveyDurations.Minutes(TimeSpan.FromSeconds(seconds));

        // Assert
        Assert.Equal(expected, text);
    }

    // A TimeSpan custom format of hours or minutes drops what is above it, so no Surveys view writes a duration that way.
    [Fact]
    public void SurveysViews_DoNotFormatADurationWithATimeSpanFormat()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var violations = new List<string>();

        // Act
        foreach (var directory in Directory.EnumerateDirectories(Path.Combine(root, "src", "Modules"), "CrestApps.OrchardCore.Surveys*"))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.cshtml", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);

                if (text.Contains("ToString(@\"h\\:", StringComparison.Ordinal) || text.Contains("ToString(@\"m\\:", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(root, path));
                }
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Modules")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
