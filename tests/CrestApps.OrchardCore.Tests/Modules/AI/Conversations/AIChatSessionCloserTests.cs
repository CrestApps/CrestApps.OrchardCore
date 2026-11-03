using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIChatSessionCloserTests
{
    [Fact]
    public void ASessionTheCustomerSpokeIn_IsClosed_AndOneTheyNeverDid_IsAbandoned()
    {
        Assert.Equal(ChatSessionStatus.Closed, AIChatSessionCloser.GetClosedStatus(
        [
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Hi, this is Sarah." },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Hello?" },
        ]));

        Assert.Equal(ChatSessionStatus.Abandoned, AIChatSessionCloser.GetClosedStatus(
        [
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Hi, this is Sarah." },
        ]));

        Assert.Equal(ChatSessionStatus.Abandoned, AIChatSessionCloser.GetClosedStatus(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    [InlineData("closed")]
    [InlineData("orphan")]
    public async Task OnlyAnActiveSessionWithItsProfile_IsClosed(string sessionId)
    {
        var sessionStore = new Mock<IAIChatSessionStore>();
        sessionStore
            .Setup(store => store.FindByIdAsync("closed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIChatSession { SessionId = "closed", ProfileId = "p1", Status = ChatSessionStatus.Closed });
        sessionStore
            .Setup(store => store.FindByIdAsync("orphan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIChatSession { SessionId = "orphan", ProfileId = "deleted", Status = ChatSessionStatus.Active });

        var promptStore = new Mock<IAIChatSessionPromptStore>();

        // The post-session analysis is never reached: a session that cannot be closed is left before it.
        var closer = new AIChatSessionCloser(
            sessionStore.Object,
            promptStore.Object,
            Mock.Of<IAIProfileManager>(),
            postCloseProcessor: null,
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IClock>(),
            NullLogger<AIChatSessionCloser>.Instance);

        Assert.False(await closer.CloseAsync(sessionId, TestContext.Current.CancellationToken));

        sessionStore.Verify(store => store.SaveAsync(It.IsAny<AIChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
        promptStore.Verify(store => store.GetPromptsAsync(It.IsAny<string>()), Times.Never);
    }
}
