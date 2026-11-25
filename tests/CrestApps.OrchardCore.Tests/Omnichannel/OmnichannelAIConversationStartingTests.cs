using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel;

public sealed class OmnichannelAIConversationStartingTests
{
    [Fact]
    public async Task StartingAsync_AnActivityWhoseHandlersAlreadyLetItStart_DoesNotRunThemAgain()
    {
        // Arrange: the first try ran the handlers, which chose a profile and a variable, and the start was then held back
        // by the sending address's limits, so the activity comes round again.
        var startingService = new Mock<IAIConversationStartingService>();
        startingService
            .Setup(service => service.StartingAsync(It.IsAny<AIConversationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var result = new AIConversationStartingResult { ProfileId = "profile-chosen" };
                result.Variables["Tier"] = "Gold";

                return result;
            });

        var starting = CreateStarting(startingService.Object);
        var activity = new OmnichannelActivity { ItemId = "activity-1" };

        // Act
        await starting.StartingAsync(activity, TestContext.Current.CancellationToken);
        var second = await starting.StartingAsync(activity, TestContext.Current.CancellationToken);

        // Assert: the handlers ran once, and the second try keeps what they decided.
        startingService.Verify(service => service.StartingAsync(It.IsAny<AIConversationContext>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(second.Declined);
        Assert.Equal("profile-chosen", activity.AIProfileId);
    }

    [Fact]
    public async Task StartingAsync_HandlersThatTimedOut_RunAgainOnTheNextTry()
    {
        // Arrange
        var startingService = new Mock<IAIConversationStartingService>();
        startingService
            .Setup(service => service.StartingAsync(It.IsAny<AIConversationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AIConversationStartingResult { TimedOut = true });

        var starting = CreateStarting(startingService.Object);
        var activity = new OmnichannelActivity { ItemId = "activity-1" };

        // Act
        await starting.StartingAsync(activity, TestContext.Current.CancellationToken);
        await starting.StartingAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        startingService.Verify(service => service.StartingAsync(It.IsAny<AIConversationContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static OmnichannelAIConversationStarting CreateStarting(IAIConversationStartingService startingService)
    {
        var contextFactory = new Mock<IOmnichannelConversationContextFactory>();
        contextFactory
            .Setup(factory => factory.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<AIChatSession>(), It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIConversationContext());

        return new OmnichannelAIConversationStarting(contextFactory.Object, startingService);
    }
}
