using CrestApps.OrchardCore.Reports;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

public sealed class ReportValueTests
{
    [Fact]
    public void UserDisplayName_WhenUserNameIsProvided_ShouldRoundTripUserName()
    {
        // Arrange
        const string userName = "agent@example.com";

        // Act
        var value = ReportValue.UserDisplayName(userName, "(Unknown user)");
        var succeeded = ReportValue.TryGetUserName(value, out var resolvedUserName);

        // Assert
        Assert.True(succeeded);
        Assert.Equal(userName, resolvedUserName);
    }

    [Fact]
    public void UserDisplayName_WhenUserNameIsMissing_ShouldReturnFallback()
    {
        // Act
        var value = ReportValue.UserDisplayName(null, "(Unknown user)");

        // Assert
        Assert.Equal("(Unknown user)", value);
        Assert.False(ReportValue.TryGetUserName(value, out _));
    }

    [Fact]
    public void TryGetUserName_WhenValueIsMalformed_ShouldReturnFalse()
    {
        // Act
        var succeeded = ReportValue.TryGetUserName("\u001Euser-display-name:not-base64\u001F", out var userName);

        // Assert
        Assert.False(succeeded);
        Assert.Null(userName);
    }
}
