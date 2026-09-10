using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsAgentAvailabilityServiceTests
{
    [Fact]
    public void Get_DefaultsToAvailable_WhenUnset()
    {
        var harness = new Harness();
        var agent = new AgentProfile { ItemId = "a1" };

        var availability = harness.Service.Get(agent);

        Assert.True(availability.Available);
        Assert.Equal(SmsAgentAvailability.DefaultMaxConcurrent, availability.EffectiveMaxConcurrent);
    }

    [Fact]
    public async Task SetAvailable_PersistsAndRoundTrips()
    {
        var harness = new Harness();
        var agent = new AgentProfile { ItemId = "a1" };

        var updated = await harness.Service.SetAvailableAsync(agent, available: false, TestContext.Current.CancellationToken);

        Assert.False(updated.Available);
        Assert.NotNull(updated.UpdatedUtc);
        harness.AgentManager.Verify(m => m.UpdateAsync(agent, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);

        // The change is persisted on the profile bag, so a subsequent read reflects it.
        Assert.False(harness.Service.Get(agent).Available);
    }

    private sealed class Harness
    {
        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public SmsAgentAvailabilityService Service { get; }

        public Harness()
        {
            AgentManager.Setup(m => m.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            Service = new SmsAgentAvailabilityService(
                AgentManager.Object,
                new Mock<ISmsAgentPresenceTracker>().Object,
                clock.Object);
        }
    }

    [Fact]
    public async Task IsAvailable_RequiresAnOpenPortal_NotJustTheFlag()
    {
        // Arrange
        // The flag alone kept an agent who closed the browser "available" for routed SMS until the five-minute
        // pickup sweep re-pooled each thread. Every message pushed at them in that window sat unanswered.
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var harness = new AvailabilityHarness(agent, portalOpen: false);

        await harness.Service.SetAvailableAsync(agent, available: true, TestContext.Current.CancellationToken);

        // Act
        var available = await harness.Service.IsAvailableAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(available);
    }

    [Fact]
    public async Task IsAvailable_RequiresTheFlag_NotJustAnOpenPortal()
    {
        // Arrange
        // An agent with the portal open has not thereby volunteered for routed SMS; the flag is a separate choice.
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var harness = new AvailabilityHarness(agent, portalOpen: true);

        await harness.Service.SetAvailableAsync(agent, available: false, TestContext.Current.CancellationToken);

        // Act
        var available = await harness.Service.IsAvailableAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(available);
    }

    [Fact]
    public async Task IsAvailable_WithTheFlagAndAnOpenPortal_IsTrue()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var harness = new AvailabilityHarness(agent, portalOpen: true);

        await harness.Service.SetAvailableAsync(agent, available: true, TestContext.Current.CancellationToken);

        // Act
        var available = await harness.Service.IsAvailableAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(available);
    }

    private sealed class AvailabilityHarness
    {
        public static readonly DateTime Now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

        public SmsAgentAvailabilityService Service { get; }

        public AvailabilityHarness(AgentProfile agent, bool portalOpen)
        {
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var presenceTracker = new Mock<ISmsAgentPresenceTracker>();
            presenceTracker
                .Setup(tracker => tracker.IsPresentAsync(agent.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(portalOpen);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(Now);

            Service = new SmsAgentAvailabilityService(
                agentManager.Object,
                presenceTracker.Object,
                clock.Object);
        }
    }

}
