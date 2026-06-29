using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentPresenceManagerServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SignInAsync_CreatesAvailableProfile_AndJoinsQueues()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        agentManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "a1" });
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, publisher.Object, clock.Object);

        // Act
        var profile = await service.SignInAsync("u1", ["q1", "q2"], [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, profile.PresenceStatus);
        Assert.Equal(2, profile.QueueIds.Count);
        agentManager.Verify(m => m.CreateAsync(It.IsAny<AgentProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_SetsOffline()
    {
        // Arrange
        var existing = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, new Mock<IContactCenterEventPublisher>().Object, clock.Object);

        // Act
        var profile = await service.SignOutAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Offline, profile.PresenceStatus);
    }
}
