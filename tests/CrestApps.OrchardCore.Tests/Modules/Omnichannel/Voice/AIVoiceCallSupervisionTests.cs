using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Handing an AI call to another AI profile.
/// </summary>
public sealed class AIVoiceCallSupervisionTests
{
    [Fact]
    public async Task ATurnBasedCall_IsHandedToTheNewProfile_WhichAnswersNext()
    {
        // Arrange
        var call = new SupervisedCall(isRealtime: false);

        // Act
        var result = await call.Supervision.SwitchProfileAsync("activity-1", "profile-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains("answers the caller next", result.Message, StringComparison.Ordinal);
        Assert.Equal("profile-2", call.Activity.AIProfileId);
        Assert.Equal("profile-2", call.Session.ProfileId);
    }

    [Fact]
    public async Task ALiveCall_KeepsItsProfile_AndTheModelIsToldNotToTransferInstead()
    {
        // Arrange
        // Live, a refused switch reached the model as done: it told the caller they were being put through, and then
        // transferred them to a person nobody had asked for.
        var call = new SupervisedCall(isRealtime: true);

        // Act
        var result = await call.Supervision.SwitchProfileAsync("activity-1", "profile-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("carry on helping them yourself", result.Message, StringComparison.Ordinal);
        Assert.Contains("do not transfer them to a person or a number instead unless they ask for one", result.Message, StringComparison.Ordinal);
        Assert.Equal("profile-1", call.Activity.AIProfileId);
        Assert.Equal("profile-1", call.Session.ProfileId);
    }

    private sealed class SupervisedCall
    {
        public SupervisedCall(bool isRealtime)
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                InteractionType = ActivityInteractionType.Automated,
                Status = ActivityStatus.InProgress,
                Channel = OmnichannelConstants.Channels.Phone,
                AIProfileId = "profile-1",
                AISessionId = "session-1",
            };
            Activity.Put(new AILiveCall { ProviderName = "Fake", ProviderCallId = "call-1", IsRealtime = isRealtime });

            Session = new AIChatSession { SessionId = "session-1", ProfileId = "profile-1" };

            var store = new Mock<IOmnichannelActivityStore>();
            store.Setup(x => x.FindByIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(Activity);

            var sessions = new Mock<IAIChatSessionManager>();
            sessions.Setup(x => x.FindByIdAsync("session-1", It.IsAny<CancellationToken>())).ReturnsAsync(Session);

            Supervision = new AIVoiceCallSupervision(
                store.Object,
                sessions.Object,
                new AIVoiceCallCommands(),
                loop: null,
                mediaResolver: null,
                new AdvanceableClock(new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)),
                NullLogger<AIVoiceCallSupervision>.Instance);
        }

        public OmnichannelActivity Activity { get; }

        public AIChatSession Session { get; }

        public AIVoiceCallSupervision Supervision { get; }
    }
}
