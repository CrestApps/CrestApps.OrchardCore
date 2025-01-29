namespace CrestApps.OrchardCore.Tests.Helpers;

public sealed class OrchardCoreHelpersTests
{
    [Theory]
    [InlineData("2.9.0", "3.0.0-preview-123")]
    [InlineData("3.0.0-preview-123", "3.0.0")]
    [InlineData("3.0.0-preview-1", "3.0.0-preview-1")]
    [InlineData("3.0.0-preview-1", "3.0.0-preview-2")]
    [InlineData("2.5.0", "2.6.0")]
    [InlineData("2.5.0", "2.5.0")]
    [InlineData("2.4.1", "2.5.0")]
    [InlineData("2.0.0", "2.5.5")]
    [InlineData("2.5.5", "2.5.5")]
    [InlineData("2.4", "2.5.0")]
    [InlineData("2.4.1", "2.5")]
    [InlineData("2.4.1", "3")]
    [InlineData("3", "3")]
    [InlineData("3.0.0-preview-1+test", "3.0.0-preview-2+test")]
    [InlineData("3.0.0+test1", "3.0.0+test1")]
    public void IsVersionGreaterOrEqual_WhenVersionIsGreater_ShouldReturnTrue(string compareTo, string currentVersion)
    {
        // Act
        var result = OrchardCoreHelpers.IsVersionGreaterOrEqual(currentVersion, compareTo);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("3.0.0-preview-3", "3.0.0-preview-2")]
    [InlineData("3.0.0", "3.0.0-preview-3")]

    [InlineData("2", "1")]
    public void IsVersionGreaterOrEqual_WhenVersionIsLess_ShouldReturnFalse(string compareTo, string currentVersion)
    {
        // Act
        var result = OrchardCoreHelpers.IsVersionGreaterOrEqual(currentVersion, compareTo);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("3.0.0-preview-2", "3.0.0-preview-3")]
    [InlineData("3.0.0-preview-3", "3.0.0")]
    [InlineData("1", "2")]
    public void IsVersionIsLess_WhenVersionIsLess_ShouldReturnTrue(string currentVersion, string compareTo)
    {
        // Act
        var result = OrchardCoreHelpers.IsVersionIsLess(currentVersion, compareTo);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("3.0.0-preview-2", "3.0.0-preview-2")]
    [InlineData("3.0.0", "3.0.0")]
    [InlineData("2", "2")]
    [InlineData("2.0", "2.0")]
    [InlineData("2.0", "2.0.0")]
    public void IsVersionIsLess_WhenVersionIsEqual_ShouldReturnFalse(string currentVersion, string compareTo)
    {
        // Act
        var result = OrchardCoreHelpers.IsVersionIsLess(currentVersion, compareTo);

        // Assert
        Assert.False(result);
    }

}
