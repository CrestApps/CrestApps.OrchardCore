using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerProviderCommandDispatchValidatorTests
{
    [Fact]
    public async Task CanDispatchAsync_WhenCurrentEligibilityAllows_ReturnsTrue()
    {
        // Arrange
        var profile = new DialerProfile { ItemId = "profile-1" };
        var activity = new OmnichannelActivity { ItemId = "activity-1" };
        var profileManager = new Mock<IDialerProfileManager>();
        profileManager
            .Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var eligibilityService = new Mock<IDialerEligibilityService>();
        eligibilityService
            .Setup(value => value.EvaluateAsync(
                It.Is<DialerEligibilityContext>(context =>
                    context.Profile == profile && context.Activity == activity),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Eligible());
        var validator = new DialerProviderCommandDispatchValidator(
            profileManager.Object,
            activityManager.Object,
            eligibilityService.Object);

        // Act
        var result = await validator.CanDispatchAsync(Command(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenPolicyNowSuppresses_ReturnsFalse()
    {
        // Arrange
        var profileManager = new Mock<IDialerProfileManager>();
        profileManager
            .Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "profile-1" });
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1" });
        var eligibilityService = new Mock<IDialerEligibilityService>();
        eligibilityService
            .Setup(value => value.EvaluateAsync(
                It.IsAny<DialerEligibilityContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.DoNotCall,
                "The destination is no longer eligible."));
        var validator = new DialerProviderCommandDispatchValidator(
            profileManager.Object,
            activityManager.Object,
            eligibilityService.Object);

        // Act
        var result = await validator.CanDispatchAsync(Command(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenProfileIsMissing_FailsClosed()
    {
        // Arrange
        var validator = new DialerProviderCommandDispatchValidator(
            new Mock<IDialerProfileManager>().Object,
            new Mock<IOmnichannelActivityManager>().Object,
            new Mock<IDialerEligibilityService>().Object);

        // Act
        var result = await validator.CanDispatchAsync(Command(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    private static ProviderCommand Command()
    {
        return new ProviderCommand
        {
            CommandId = "command-1",
            DialerProfileId = "profile-1",
            ActivityItemId = "activity-1",
        };
    }
}
