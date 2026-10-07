using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
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
    public async Task ValidatingAsync_WhenTheModeIsPredictiveWithoutThePacedFeature_Fails()
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Predictive;

        // Act
        var context = await ValidateAsync(profile, automatedDialerEnabled: false);

        // Assert
        var error = Assert.Single(context.Result.Errors, error => error.MemberNames.Contains(nameof(DialerProfile.Mode)));
        Assert.Equal("Enable the Contact Center Paced Dialing feature before using Power, Progressive or Predictive dialing.", error.ErrorMessage);
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheModeIsPredictiveWithThePacedFeature_Succeeds()
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Predictive;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenAnOverDialingProfileHasEverySafeguard_Succeeds()
    {
        // Act
        var context = await ValidateAsync(CreateOverDialProfile());

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenOverDialingDoesNotEnforceTheCap_Fails()
    {
        // Arrange
        var profile = CreateOverDialProfile();
        profile.EnforceAbandonmentCap = false;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.EnforceAbandonmentCap));
    }

    [Fact]
    public async Task ValidatingAsync_WhenOverDialingHasNeitherTheCapNorTheMessage_FailsForBoth()
    {
        // Arrange
        var profile = CreateOverDialProfile();
        profile.EnforceAbandonmentCap = false;
        profile.SafeHarborEnabled = false;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.EnforceAbandonmentCap));
        AssertFailedFor(context, nameof(DialerProfile.SafeHarborEnabled));
    }

    [Fact]
    public async Task ValidatingAsync_WhenOverDialingCapsAbandonmentWithoutTheMessage_Fails()
    {
        // Arrange
        var profile = CreateOverDialProfile();
        profile.SafeHarborEnabled = false;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.SafeHarborEnabled));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    public async Task ValidatingAsync_WhenTheOverDialTargetIsNotBelowTheCap_Fails(double target, double cap)
    {
        // Arrange
        var profile = CreateOverDialProfile();
        profile.TargetAbandonmentRatePercent = target;
        profile.MaxAbandonmentRatePercent = cap;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.TargetAbandonmentRatePercent));
    }

    [Fact]
    public async Task ValidatingAsync_WhenOverDialingHasAZeroCap_Fails()
    {
        // Arrange
        var profile = CreateOverDialProfile();
        profile.MaxAbandonmentRatePercent = 0;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.MaxAbandonmentRatePercent));
    }

    [Fact]
    public async Task ValidatingAsync_WhenAReservedPredictiveProfileHasATargetAboveTheCap_Succeeds()
    {
        // Arrange: the target only steers over-dialing, so a profile that reserves an agent per call is not held to it.
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Predictive;
        profile.TargetAbandonmentRatePercent = 5;
        profile.MaxAbandonmentRatePercent = 3;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    public static TheoryData<string, string> PredictiveRangeCases => new()
    {
        { "pacing model undefined", nameof(DialerProfile.PredictivePacingModel) },
        { "target zero", nameof(DialerProfile.TargetAbandonmentRatePercent) },
        { "target above one hundred", nameof(DialerProfile.TargetAbandonmentRatePercent) },
        { "target not a number", nameof(DialerProfile.TargetAbandonmentRatePercent) },
        { "lines below one", nameof(DialerProfile.MaxLinesPerAgent) },
        { "lines above the ceiling", nameof(DialerProfile.MaxLinesPerAgent) },
        { "lines not a number", nameof(DialerProfile.MaxLinesPerAgent) },
        { "no calls in flight", nameof(DialerProfile.MaxCallsInFlight) },
        { "too many calls in flight", nameof(DialerProfile.MaxCallsInFlight) },
        { "answer rate floor too low", nameof(DialerProfile.AnswerRateSampleFloor) },
        { "answer rate floor too high", nameof(DialerProfile.AnswerRateSampleFloor) },
        { "answer rate window too short", nameof(DialerProfile.AnswerRateWindowMinutes) },
        { "answer rate window too long", nameof(DialerProfile.AnswerRateWindowMinutes) },
        { "credit negative", nameof(DialerProfile.FreeUpCreditPercent) },
        { "credit above one hundred", nameof(DialerProfile.FreeUpCreditPercent) },
        { "connect wait negative", nameof(DialerProfile.ConnectWaitMilliseconds) },
        { "connect wait too long", nameof(DialerProfile.ConnectWaitMilliseconds) },
    };

    [Theory]
    [MemberData(nameof(PredictiveRangeCases))]
    public async Task ValidatingAsync_WhenAPredictiveSettingIsOutOfRange_Fails(string scenario, string memberName)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Predictive;
        ApplyPredictiveScenario(profile, scenario);

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, memberName);
    }

    [Theory]
    [MemberData(nameof(PredictiveRangeCases))]
    public async Task ValidatingAsync_WhenAPredictiveSettingIsOutOfRangeOnAPowerProfile_Succeeds(string scenario, string memberName)
    {
        // Arrange: a setting the mode never uses is not a reason to refuse the profile.
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Power;
        ApplyPredictiveScenario(profile, scenario);

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.True(context.Result.Succeeded, memberName);
    }

    [Theory]
    [InlineData(1, 1, 10, 5, 0, 0)]
    [InlineData(5, 1000, 10000, 240, 100, 0)]
    public async Task ValidatingAsync_WhenPredictiveSettingsAreOnTheirBoundaries_Succeeds(
        double linesPerAgent,
        int callsInFlight,
        int sampleFloor,
        int windowMinutes,
        int creditPercent,
        int connectWaitMilliseconds)
    {
        // Arrange
        var profile = CreateOverDialProfile();
        profile.MaxLinesPerAgent = linesPerAgent;
        profile.MaxCallsInFlight = callsInFlight;
        profile.AnswerRateSampleFloor = sampleFloor;
        profile.AnswerRateWindowMinutes = windowMinutes;
        profile.FreeUpCreditPercent = creditPercent;
        profile.ConnectWaitMilliseconds = connectWaitMilliseconds;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public void NewProfile_DefaultsToReservingAnAgentPerCall()
    {
        // Act
        var profile = new DialerProfile();

        // Assert
        Assert.Equal(PredictivePacingModel.ReservedPerCall, profile.PredictivePacingModel);
        Assert.True(profile.TargetAbandonmentRatePercent < profile.MaxAbandonmentRatePercent);
        Assert.False(profile.CreditAgentsFreeingUp);
        Assert.Equal(0, profile.ConnectWaitMilliseconds);
        Assert.True(profile.AbandonedRetryRequiresAgent);
    }

    private static void ApplyPredictiveScenario(DialerProfile profile, string scenario)
    {
        switch (scenario)
        {
            case "pacing model undefined": profile.PredictivePacingModel = (PredictivePacingModel)42; break;
            case "target zero": profile.TargetAbandonmentRatePercent = 0; break;
            case "target above one hundred": profile.TargetAbandonmentRatePercent = 101; break;
            case "target not a number": profile.TargetAbandonmentRatePercent = double.NaN; break;
            case "lines below one": profile.MaxLinesPerAgent = 0.9; break;
            case "lines above the ceiling": profile.MaxLinesPerAgent = PredictiveDialingDefaults.MaxLinesPerAgent + 0.1; break;
            case "lines not a number": profile.MaxLinesPerAgent = double.NaN; break;
            case "no calls in flight": profile.MaxCallsInFlight = 0; break;
            case "too many calls in flight": profile.MaxCallsInFlight = PredictiveDialingDefaults.MaxCallsInFlight + 1; break;
            case "answer rate floor too low": profile.AnswerRateSampleFloor = PredictiveDialingDefaults.MinAnswerRateSampleFloor - 1; break;
            case "answer rate floor too high": profile.AnswerRateSampleFloor = PredictiveDialingDefaults.MaxAnswerRateSampleFloor + 1; break;
            case "answer rate window too short": profile.AnswerRateWindowMinutes = PredictiveDialingDefaults.MinAnswerRateWindowMinutes - 1; break;
            case "answer rate window too long": profile.AnswerRateWindowMinutes = PredictiveDialingDefaults.MaxAnswerRateWindowMinutes + 1; break;
            case "credit negative": profile.FreeUpCreditPercent = -1; break;
            case "credit above one hundred": profile.FreeUpCreditPercent = 101; break;
            case "connect wait negative": profile.ConnectWaitMilliseconds = -1; break;
            case "connect wait too long": profile.ConnectWaitMilliseconds = PredictiveDialingDefaults.MaxConnectWaitMilliseconds + 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    [Fact]
    public async Task ValidatingAsync_WhenAConnectWaitHasNoMeasuredConnectTimes_Fails()
    {
        // Arrange: a profile that has not connected enough calls to show the wait is safe.
        var profile = CreateOverDialProfile();
        profile.ItemId = "profile-1";
        profile.ConnectWaitMilliseconds = 500;
        var statistics = new DialerPacingStatistics
        {
            P95ConnectLatency = TimeSpan.FromMilliseconds(400),
            ConnectLatencySamples = PredictiveConnectWait.MinimumLatencySamples - 1,
        };

        // Act
        var context = await ValidateAsync(profile, statistics: statistics);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.ConnectWaitMilliseconds));
    }

    [Fact]
    public async Task ValidatingAsync_WhenANewProfileAsksForAConnectWait_Fails()
    {
        // Arrange: nothing can have been measured for a profile not saved yet.
        var profile = CreateOverDialProfile();
        profile.ConnectWaitMilliseconds = 500;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        AssertFailedFor(context, nameof(DialerProfile.ConnectWaitMilliseconds));
    }

    [Theory]
    [InlineData(500, 1500, true)]
    [InlineData(600, 1500, false)]
    [InlineData(1900, 200, false)]
    public async Task ValidatingAsync_AConnectWait_IsAllowedOnlyWhenItPlusTheMeasuredConnectTimeStaysWithinTwoSeconds(int p95Milliseconds, int waitMilliseconds, bool succeeds)
    {
        // Arrange
        var profile = CreateOverDialProfile();
        profile.ItemId = "profile-1";
        profile.ConnectWaitMilliseconds = waitMilliseconds;
        var statistics = new DialerPacingStatistics
        {
            P95ConnectLatency = TimeSpan.FromMilliseconds(p95Milliseconds),
            ConnectLatencySamples = PredictiveConnectWait.MinimumLatencySamples,
        };

        // Act
        var context = await ValidateAsync(profile, statistics: statistics);

        // Assert
        if (succeeds)
        {
            Assert.True(context.Result.Succeeded);
        }
        else
        {
            AssertFailedFor(context, nameof(DialerProfile.ConnectWaitMilliseconds));
        }
    }

    [Fact]
    public async Task ValidatingAsync_AConnectWaitOnAPowerProfile_IsNotChecked()
    {
        // Arrange: a setting the mode never uses.
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Power;
        profile.ConnectWaitMilliseconds = 500;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    private static DialerProfile CreateOverDialProfile()
    {
        var profile = CreateValidProfile();
        profile.Mode = DialerMode.Predictive;
        profile.PredictivePacingModel = PredictivePacingModel.OverDial;
        profile.EnforceAbandonmentCap = true;
        profile.MaxAbandonmentRatePercent = 3;
        profile.TargetAbandonmentRatePercent = 2;
        profile.SafeHarborEnabled = true;
        profile.SafeHarborMessage = DialerAbandonment.DefaultMessage;

        return profile;
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

    private static async Task<ValidatingContext<DialerProfile>> ValidateAsync(
        DialerProfile profile,
        bool automatedDialerEnabled = true,
        DialerPacingStatistics statistics = null)
    {
        var context = new ValidatingContext<DialerProfile>(profile);

        await CreateHandler(automatedDialerEnabled, statistics).ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static void AssertFailedFor(ValidatingContext<DialerProfile> context, string memberName)
    {
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(memberName));
    }

    private static DialerProfileHandler CreateHandler(bool automatedDialerEnabled, DialerPacingStatistics statistics = null)
    {
        var features = new List<IFeatureInfo>();

        if (automatedDialerEnabled)
        {
            var feature = new Mock<IFeatureInfo>();

            feature.SetupGet(x => x.Id).Returns(ContactCenterConstants.Feature.DialerPaced);
            features.Add(feature.Object);
        }

        var featuresManager = new Mock<IShellFeaturesManager>();

        featuresManager
            .Setup(x => x.GetEnabledFeaturesAsync())
            .ReturnsAsync(features);

        return new DialerProfileHandler(
            new Mock<IClock>().Object,
            featuresManager.Object,
            new DefaultPhoneNumberService(),
            [CreatePacingStatistics(statistics)],
            new PassThroughStringLocalizer<DialerProfileHandler>());
    }

    private static IDialerPacingStatisticsProvider CreatePacingStatistics(DialerPacingStatistics statistics)
    {
        var provider = new Mock<IDialerPacingStatisticsProvider>();
        provider
            .Setup(value => value.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);

        return provider.Object;
    }

    /// <summary>
    /// A caller id that is not a real phone number is rejected by the voice provider the instant it is dialed,
    /// with no call placed and nothing obvious to point at. The rule lives on the handler so a recipe import is
    /// held to it as well as the editor.
    /// </summary>
    [Theory]
    [InlineData("CrestApps")]
    [InlineData("not a number")]
    [InlineData("+1555")]
    public async Task ValidatingAsync_WhenTheCallerIdIsNotAPhoneNumber_Fails(string callerId)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.CallerId = callerId;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("+14155552671")]
    public async Task ValidatingAsync_WhenTheCallerIdIsBlankOrAPhoneNumber_Succeeds(string callerId)
    {
        // Arrange
        var profile = CreateValidProfile();
        profile.CallerId = callerId;

        // Act
        var context = await ValidateAsync(profile);

        // Assert
        Assert.True(context.Result.Succeeded);
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
