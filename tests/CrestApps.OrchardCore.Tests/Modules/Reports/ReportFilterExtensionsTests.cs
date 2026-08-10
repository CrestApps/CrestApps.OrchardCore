using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

public sealed class ReportFilterExtensionsTests
{
    private enum SampleStatus
    {
        None,
        Active,
        Closed,
    }

    [Fact]
    public void TryGet_WhenKeyIsMissing_ShouldReturnFalse()
    {
        // Arrange
        var filter = new ReportFilter();

        // Act
        var found = filter.TryGet<string>("Missing", out var value);

        // Assert
        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void SetThenTryGet_WithString_ShouldRoundTrip()
    {
        // Arrange
        var filter = new ReportFilter();

        // Act
        filter.Set("CampaignId", "abc123");
        var found = filter.TryGet<string>("CampaignId", out var value);

        // Assert
        Assert.True(found);
        Assert.Equal("abc123", value);
    }

    [Fact]
    public void GetOrDefault_WhenKeyIsMissing_ShouldReturnDefault()
    {
        // Arrange
        var filter = new ReportFilter();

        // Act
        var value = filter.GetOrDefault<string>("Missing");

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void GetOrDefault_WhenKeyIsPresent_ShouldReturnValue()
    {
        // Arrange
        var filter = new ReportFilter();
        filter.Set("Take", 25);

        // Act
        var value = filter.GetOrDefault<int>("Take");

        // Assert
        Assert.Equal(25, value);
    }

    [Fact]
    public void SetThenTryGet_WithInteger_ShouldRoundTrip()
    {
        // Arrange
        var filter = new ReportFilter();

        // Act
        filter.Set("Take", 25);
        var found = filter.TryGet<int>("Take", out var value);

        // Assert
        Assert.True(found);
        Assert.Equal(25, value);
    }

    [Fact]
    public void SetThenTryGet_WithEnumStoredAsString_ShouldParseEnum()
    {
        // Arrange
        var filter = new ReportFilter();

        // Act
        filter.Set("Status", "Active");
        var found = filter.TryGet<SampleStatus>("Status", out var value);

        // Assert
        Assert.True(found);
        Assert.Equal(SampleStatus.Active, value);
    }

    [Fact]
    public void Set_WithNullValue_ShouldRemoveKey()
    {
        // Arrange
        var filter = new ReportFilter();
        filter.Set("Channel", "phone");

        // Act
        filter.Set<string>("Channel", null);

        // Assert
        Assert.False(filter.TryGet<string>("Channel", out _));
    }

    [Fact]
    public void Set_WithEmptyString_ShouldRemoveKey()
    {
        // Arrange
        var filter = new ReportFilter();
        filter.Set("Channel", "phone");

        // Act
        filter.Set("Channel", string.Empty);

        // Assert
        Assert.False(filter.TryGet<string>("Channel", out _));
    }

    [Fact]
    public void SetDateRange_ThenGetDateRange_ShouldRoundTripAsUtc()
    {
        // Arrange
        var filter = new ReportFilter();
        var range = new ReportDateRange
        {
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc),
            Key = "last30",
        };

        // Act
        filter.SetDateRange(range);
        var resolved = filter.GetDateRange();

        // Assert
        Assert.Equal(range.FromUtc, resolved.FromUtc);
        Assert.Equal(range.ToUtc, resolved.ToUtc);
        Assert.Equal(DateTimeKind.Utc, resolved.FromUtc.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, resolved.ToUtc.Value.Kind);
        Assert.Equal("last30", resolved.Key);
    }

    [Fact]
    public void GetDateRange_WhenNotContributed_ShouldReturnUnsetBounds()
    {
        // Arrange
        var filter = new ReportFilter();

        // Act
        var resolved = filter.GetDateRange();

        // Assert
        Assert.Null(resolved.FromUtc);
        Assert.Null(resolved.ToUtc);
        Assert.Null(resolved.Key);
    }
}
