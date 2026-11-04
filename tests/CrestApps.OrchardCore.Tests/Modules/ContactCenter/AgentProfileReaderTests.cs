using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentProfileReaderTests
{
    [Fact]
    public async Task FindAgentIdByUserIdAsync_ReturnsTheIdOfTheUsersAgentProfile()
    {
        // Arrange
        var profiles = new Mock<IAgentProfileManager>();
        profiles
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "profile-1" });
        var reader = new AgentProfileReader(profiles.Object);

        // Act
        var agentId = await reader.FindAgentIdByUserIdAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("profile-1", agentId);
    }

    [Fact]
    public async Task FindAgentIdByUserIdAsync_WhenTheUserIsNotAnAgent_ReturnsNull()
    {
        // Arrange
        var profiles = new Mock<IAgentProfileManager>();
        profiles
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentProfile)null);
        var reader = new AgentProfileReader(profiles.Object);

        // Act
        var agentId = await reader.FindAgentIdByUserIdAsync("user-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(agentId);
    }
}
