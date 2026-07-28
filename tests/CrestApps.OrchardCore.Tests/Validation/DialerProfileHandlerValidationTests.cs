using System.Collections.Generic;
using System.Linq;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Pins the dialer rules to the handler, where every write path runs them, rather than to the editor.
/// </summary>
public class DialerProfileHandlerValidationTests
{
    [Fact]
    public async Task ValidatingAsync_WhenTheProfileIsWellFormed_Succeeds()
    {
        // Arrange
        var handler = CreateHandler(automatedDialerEnabled: true);
        var context = new ValidatingContext<DialerProfile>(CreateValidProfile());

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidatingAsync_WhenTheNameIsMissing_Fails(string name)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Name = name;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.Name));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheModeIsPredictive_Fails()
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Predictive;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.Mode));
    }

    [Theory]
    [InlineData(DialerMode.Power)]
    [InlineData(DialerMode.Progressive)]
    public async Task ValidatingAsync_WhenAnAutomatedModeRunsWithoutTheAutomatedDialerFeature_Fails(DialerMode mode)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Mode = mode;

        // Act
        var context = await ValidateAsync(profile, automatedDialerEnabled: false);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.Mode));
    }

    [Theory]
    [InlineData(DialerMode.Manual)]
    [InlineData(DialerMode.Preview)]
    public async Task ValidatingAsync_WhenAManualModeRunsWithoutTheAutomatedDialerFeature_Succeeds(DialerMode mode)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Mode = mode;

        // Act
        var context = await ValidateAsync(profile, automatedDialerEnabled: false);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(PowerDialerStrategy.MaxCallsPerAgent + 1)]
    public async Task ValidatingAsync_WhenCallsPerAgentIsOutOfRange_Fails(int callsPerAgent)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.CallsPerAgent = callsPerAgent;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.CallsPerAgent));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(PowerDialerStrategy.MaxCallsPerAgent)]
    public async Task ValidatingAsync_WhenCallsPerAgentIsOnTheBoundary_Succeeds(int callsPerAgent)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.CallsPerAgent = callsPerAgent;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheCallingWindowIsEnforcedWithoutACalendar_Fails()
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.EnforceCallingWindow = true;
        profile.CallingCalendarId = null;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.CallingCalendarId));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ValidatingAsync_WhenTheAbandonmentRateIsOutOfRange_Fails(double rate)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.MaxAbandonmentRatePercent = rate;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.MaxAbandonmentRatePercent));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheAbandonmentSampleFloorIsNegative_Fails()
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.AbandonmentSampleFloor = -1;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.AbandonmentSampleFloor));
    }

    [Fact]
    public async Task ValidatingAsync_WhenAnAutomatedModeCapsAbandonmentWithoutSafeHarbor_Fails()
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Power;
        profile.EnforceAbandonmentCap = true;
        profile.SafeHarborEnabled = false;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.SafeHarborEnabled));
    }

    [Fact]
    public async Task ValidatingAsync_WhenSafeHarborIsEnabledWithoutAnAnnouncement_Fails()
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.SafeHarborEnabled = true;
        profile.SafeHarborMessage = null;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.SafeHarborMessage));
    }

    private static async Task<ValidatingContext<DialerProfile>> ValidateAsync(DialerProfile profile, bool automatedDialerEnabled = true)
    {
        var context = new ValidatingContext<DialerProfile>(profile);

        await CreateHandler(automatedDialerEnabled).ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static void AssertFailedFor(ValidatingContext<DialerProfile> context, string memberName)
    {
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(memberName));
    }

    private static DialerProfileHandler CreateHandler(bool automatedDialerEnabled)
    {
        var features = new List<IFeatureInfo>();

        if (automatedDialerEnabled)
        {
            var feature = new Mock<IFeatureInfo>();

            feature.SetupGet(x => x.Id).Returns(ContactCenterConstants.Feature.DialerAutomated);
            features.Add(feature.Object);
        }

        var featuresManager = new Mock<IShellFeaturesManager>();

        featuresManager
            .Setup(x => x.GetEnabledFeaturesAsync())
            .ReturnsAsync(features);

        return new DialerProfileHandler(
            new Mock<IClock>().Object,
            featuresManager.Object,
            new PassThroughStringLocalizer<DialerProfileHandler>());
    }

    private static DialerProfile CreateValidProfile()
    {
        return new DialerProfile
        {
            Name = "Outbound",
            Mode = DialerMode.Manual,
            CallsPerAgent = 1,
        };
    }
}
