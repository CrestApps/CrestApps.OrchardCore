using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Services;

public sealed class OmnichannelAutomationHelperTests
{
    [Theory]
    [InlineData(ActivityInteractionType.Automated, false, ActivityStatus.NotStated)]
    [InlineData(ActivityInteractionType.Automated, true, ActivityStatus.NotStated)]
    [InlineData(ActivityInteractionType.Manual, false, ActivityStatus.Scheduled)]
    [InlineData(ActivityInteractionType.Manual, true, ActivityStatus.NotStated)]
    public void GetInitialActivityStatus_WhenActivityIsLoaded_ShouldReturnExpectedStatus(
        ActivityInteractionType interactionType,
        bool hasAssignedUser,
        ActivityStatus expectedStatus)
    {
        // Act
        var status = OmnichannelAutomationHelper.GetInitialActivityStatus(interactionType, hasAssignedUser);

        // Assert
        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void ResolveNoResponseDeadline_WhenTimeoutIsConfigured_ShouldAddTimeout()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var flowSettings = new SubjectFlowSettings
        {
            NoResponseTimeoutInMinutes = 15,
        };

        // Act
        var deadline = OmnichannelAutomationHelper.ResolveNoResponseDeadline(flowSettings, utcNow);

        // Assert
        Assert.Equal(utcNow.AddMinutes(15), deadline);
    }

    [Fact]
    public void HasNoResponseTimeout_WhenTimeoutIsMissing_ShouldReturnFalse()
    {
        // Arrange
        var flowSettings = new SubjectFlowSettings();

        // Act
        var result = OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings);

        // Assert
        Assert.False(result);
    }
}
