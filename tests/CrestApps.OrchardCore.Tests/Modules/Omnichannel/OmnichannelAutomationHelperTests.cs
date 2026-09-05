using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Locks the pacing, timeout, and load-time settings logic that governs automated SMS and phone conversations, so a
/// later refactor cannot silently change how fast the automation replies, when it gives up, or which settings a loaded
/// activity inherits from the batch and subject flow.
/// </summary>
public sealed class OmnichannelAutomationHelperTests
{
    // --- Reply-delay resolution -------------------------------------------------------------------------------------

    [Fact]
    public void ResolveResponseDelay_WhenModeIsNone_ReturnsNull()
    {
        Assert.Null(OmnichannelAutomationHelper.ResolveResponseDelay(OmnichannelResponseDelayMode.None, 5, 2));
    }

    [Fact]
    public void ResolveResponseDelay_WhenModeIsFixed_ReturnsTheConfiguredSeconds()
    {
        var delay = OmnichannelAutomationHelper.ResolveResponseDelay(OmnichannelResponseDelayMode.Fixed, 5, 2);

        Assert.Equal(TimeSpan.FromSeconds(5), delay);
    }

    [Fact]
    public void ResolveResponseDelay_WhenModeIsFixedAndSecondsAreZero_ReturnsNull()
    {
        Assert.Null(OmnichannelAutomationHelper.ResolveResponseDelay(OmnichannelResponseDelayMode.Fixed, 0, 2));
    }

    [Fact]
    public void ResolveResponseDelay_WhenModeIsRandom_StaysWithinTheJitterBand()
    {
        // Run enough times that a value out of band would almost certainly be caught.
        for (var i = 0; i < 200; i++)
        {
            var delay = OmnichannelAutomationHelper.ResolveResponseDelay(OmnichannelResponseDelayMode.Random, 10, 3);

            Assert.NotNull(delay);
            Assert.InRange(delay.Value.TotalSeconds, 7, 13);
        }
    }

    [Fact]
    public void ResolveResponseDelay_WhenModeIsRandomAndResolvesToZero_ReturnsNull()
    {
        Assert.Null(OmnichannelAutomationHelper.ResolveResponseDelay(OmnichannelResponseDelayMode.Random, 0, 0));
    }

    // --- Humanized reading (settle) delay --------------------------------------------------------------------------

    [Fact]
    public void ResolveHumanizedReadingDelay_WithNoConfiguredDelayAndNoInput_IsTheThreeSecondFloor()
    {
        var delay = OmnichannelAutomationHelper.ResolveHumanizedReadingDelay(null, 0);

        Assert.Equal(TimeSpan.FromSeconds(OmnichannelAutomationHelper.MinimumHumanizedReplyDelaySeconds), delay);
        Assert.Equal(3, delay.TotalSeconds);
    }

    [Fact]
    public void ResolveHumanizedReadingDelay_AddsReadingTimeProportionalToInboundLength()
    {
        // floor 3s + 100 chars / 20 cps = 5s => 8s.
        var delay = OmnichannelAutomationHelper.ResolveHumanizedReadingDelay(null, 100);

        Assert.Equal(TimeSpan.FromSeconds(8), delay);
    }

    [Fact]
    public void ResolveHumanizedReadingDelay_CapsTheReadingContributionForVeryLongInput()
    {
        // floor 3s + 400/20 = 23s, but reading is capped at floor + 15s => 18s.
        var delay = OmnichannelAutomationHelper.ResolveHumanizedReadingDelay(null, 400);

        Assert.Equal(TimeSpan.FromSeconds(18), delay);
    }

    [Fact]
    public void ResolveHumanizedReadingDelay_HonorsAConfiguredDelayAsTheFloor()
    {
        var delay = OmnichannelAutomationHelper.ResolveHumanizedReadingDelay(TimeSpan.FromSeconds(10), 0);

        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    // --- Humanized typing delay ------------------------------------------------------------------------------------

    [Fact]
    public void ResolveHumanizedTypingDelay_IsProportionalToReplyLength()
    {
        // 60 chars / 12 cps = 5s.
        Assert.Equal(TimeSpan.FromSeconds(5), OmnichannelAutomationHelper.ResolveHumanizedTypingDelay(60));
    }

    [Fact]
    public void ResolveHumanizedTypingDelay_IsCappedForLongReplies()
    {
        // 1000 / 12 = ~83s, capped at 8s.
        Assert.Equal(TimeSpan.FromSeconds(8), OmnichannelAutomationHelper.ResolveHumanizedTypingDelay(1000));
    }

    [Fact]
    public void ResolveHumanizedTypingDelay_ForEmptyReply_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, OmnichannelAutomationHelper.ResolveHumanizedTypingDelay(0));
    }

    // --- No-response timeout ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(30, true)]
    public void HasNoResponseTimeout_ReflectsAPositiveTimeout(int? minutes, bool expected)
    {
        var settings = new SubjectFlowSettings { NoResponseTimeoutInMinutes = minutes };

        Assert.Equal(expected, OmnichannelAutomationHelper.HasNoResponseTimeout(settings));
    }

    [Fact]
    public void ResolveNoResponseDeadline_WithATimeout_AddsItToNow()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var settings = new SubjectFlowSettings { NoResponseTimeoutInMinutes = 30 };

        Assert.Equal(now.AddMinutes(30), OmnichannelAutomationHelper.ResolveNoResponseDeadline(settings, now));
    }

    [Fact]
    public void ResolveNoResponseDeadline_WithoutATimeout_ReturnsNow()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var settings = new SubjectFlowSettings { NoResponseTimeoutInMinutes = null };

        Assert.Equal(now, OmnichannelAutomationHelper.ResolveNoResponseDeadline(settings, now));
    }

    // --- Initial activity status -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(ActivityInteractionType.Automated, false, ActivityStatus.NotStated)]
    [InlineData(ActivityInteractionType.Automated, true, ActivityStatus.NotStated)]
    [InlineData(ActivityInteractionType.Manual, true, ActivityStatus.NotStated)]
    [InlineData(ActivityInteractionType.Manual, false, ActivityStatus.Scheduled)]
    public void GetInitialActivityStatus_FollowsInteractionAndAssignment(
        ActivityInteractionType interactionType,
        bool hasAssignedUser,
        ActivityStatus expected)
    {
        Assert.Equal(expected, OmnichannelAutomationHelper.GetInitialActivityStatus(interactionType, hasAssignedUser));
    }

    // --- Load-time settings snapshot -------------------------------------------------------------------------------

    [Fact]
    public void ResolveActivitySettings_SnapshotsBatchPacingAndReEngagementChoices()
    {
        var batch = new OmnichannelActivityBatch
        {
            ResponseDelayMode = OmnichannelResponseDelayMode.Fixed,
            ResponseDelaySeconds = 5,
            ResponseDelayJitterSeconds = 2,
            AllowAIToUpdateContact = true,
            AllowAIToUpdateSubject = false,
            BusinessHoursCalendarId = "calendar-1",
            CadenceId = "cadence-1",
        };

        var settings = OmnichannelAutomationHelper.ResolveActivitySettings(batch, flowSettings: null);

        Assert.Equal(OmnichannelResponseDelayMode.Fixed, settings.ResponseDelayMode);
        Assert.Equal(5, settings.ResponseDelaySeconds);
        Assert.Equal(2, settings.ResponseDelayJitterSeconds);
        Assert.True(settings.AllowAIToUpdateContact);
        Assert.False(settings.AllowAIToUpdateSubject);
        Assert.Equal("calendar-1", settings.BusinessHoursCalendarId);
        Assert.Equal("cadence-1", settings.CadenceId);
    }

    [Fact]
    public void ResolveActivitySettings_FallsBackToTheSubjectFlowProfileWhenTheBatchHasNone()
    {
        var batch = new OmnichannelActivityBatch { AIProfileId = null };
        var flow = new SubjectFlowSettings { ProfileId = "flow-profile" };

        var settings = OmnichannelAutomationHelper.ResolveActivitySettings(batch, flow);

        Assert.Equal("flow-profile", settings.AIProfileId);
    }

    [Fact]
    public void ResolveActivitySettings_PrefersTheBatchProfileOverTheSubjectFlow()
    {
        var batch = new OmnichannelActivityBatch { AIProfileId = "batch-profile" };
        var flow = new SubjectFlowSettings { ProfileId = "flow-profile" };

        var settings = OmnichannelAutomationHelper.ResolveActivitySettings(batch, flow);

        Assert.Equal("batch-profile", settings.AIProfileId);
    }
}
