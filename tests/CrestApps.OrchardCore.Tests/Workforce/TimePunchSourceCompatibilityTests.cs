using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// A punch source is an open string, so the contact center bridge names its own source; punches and policies saved
/// while the source was an enum are still read.
/// </summary>
public sealed class TimePunchSourceCompatibilityTests
{
    [Theory]
    [InlineData("0", TimePunchSource.Web)]
    [InlineData("3", "ContactCenter")]
    [InlineData("5", TimePunchSource.Manager)]
    [InlineData("\"Kiosk\"", TimePunchSource.Kiosk)]
    [InlineData("\"Badge\"", "Badge")]
    public void APunchSavedWithANumberOrAName_IsRead(string storedSource, string expected)
    {
        // Act
        var punch = JsonSerializer.Deserialize<TimePunch>($$"""{"WorkerId":"w1","Source":{{storedSource}}}""");

        // Assert
        Assert.Equal(expected, punch.Source);
        Assert.Contains($"\"Source\":\"{expected}\"", JsonSerializer.Serialize(punch), StringComparison.Ordinal);
    }

    [Fact]
    public void APolicySavedWithNumbers_AllowsTheSameSources()
    {
        // Act
        var policy = JsonSerializer.Deserialize<TimeClockPolicy>("""{"AllowedSources":[2,3]}""");

        // Assert
        Assert.Equal([TimePunchSource.Kiosk, "ContactCenter"], policy.AllowedSources);
        Assert.True(policy.Allows("ContactCenter"));
        Assert.True(policy.Allows(TimePunchSource.Manager));
        Assert.False(policy.Allows(TimePunchSource.Web));
        Assert.Contains("\"AllowedSources\":[\"Kiosk\",\"ContactCenter\"]", JsonSerializer.Serialize(policy), StringComparison.Ordinal);
    }
}
