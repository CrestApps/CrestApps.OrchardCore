using System.IO;
using CrestApps.OrchardCore.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Time zone pickers in the time, work and people modules come from <see cref="ITimeZoneSelectListProvider"/>, so they
/// show the same zones everywhere, with the friendly names the Time Zones feature maps, instead of a list built from
/// <see cref="IClock.GetTimeZones"/>.
/// </summary>
public sealed class TimeZonePickerArchitectureTests
{
    private static readonly string[] _guardedModules =
    [
        "CrestApps.OrchardCore.Assets",
        "CrestApps.OrchardCore.Calendars",
        "CrestApps.OrchardCore.Dispatch",
        "CrestApps.OrchardCore.Geo",
        "CrestApps.OrchardCore.Scheduling",
        "CrestApps.OrchardCore.Skills",
        "CrestApps.OrchardCore.Teams",
        "CrestApps.OrchardCore.TimeTracker",
        "CrestApps.OrchardCore.Workforce",
        "CrestApps.OrchardCore.Workforce.ContactCenter",
        "CrestApps.OrchardCore.WorkOrders",
    ];

    [Fact]
    public void TimeZonePickers_AreNotBuiltFromTheClocksTimeZones()
    {
        // Arrange
        var root = Path.Combine(FindRepositoryRoot(), "src", "Modules");
        var violations = new List<string>();

        // Act
        foreach (var module in _guardedModules)
        {
            var directory = Path.Combine(root, module);

            Assert.True(Directory.Exists(directory), $"The guarded module '{module}' no longer exists.");

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);

                for (var index = 0; index < lines.Length; index++)
                {
                    if (lines[index].Contains("GetTimeZones()", StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetRelativePath(root, file)}({index + 1}): {lines[index].Trim()}");
                    }
                }
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public async Task GetTimeZoneOptionsAsync_MarksTheSelectedZone_AndKeepsOneTheListDoesNotHave()
    {
        // Arrange
        var provider = new Mock<ITimeZoneSelectListProvider>();

        provider
            .Setup(p => p.GetTimeZoneSelectListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new KeyValuePair<string, string>("America/New_York", "Eastern Time"),
                new KeyValuePair<string, string>("Europe/Paris", "Paris"),
            });

        // Act
        var listed = await provider.Object.GetTimeZoneOptionsAsync("america/new_york", TestContext.Current.CancellationToken);
        var missing = await provider.Object.GetTimeZoneOptionsAsync("Asia/Tokyo", TestContext.Current.CancellationToken);
        var none = await provider.Object.GetTimeZoneOptionsAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Eastern Time", "Paris"], listed.Select(option => option.Text));
        Assert.Equal("America/New_York", Assert.Single(listed, option => option.Selected).Value);
        Assert.Equal(["Asia/Tokyo", "America/New_York", "Europe/Paris"], missing.Select(option => option.Value));
        Assert.True(missing[0].Selected);
        Assert.DoesNotContain(none, option => option.Selected);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
