using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.Extensions.Options;
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
                new Mock<IAgentSessionManager>().Object,
                new OptionsWrapper<AgentAvailabilityOptions>(new AgentAvailabilityOptions()),
                clock.Object);
        }
    }

    [Fact]
    public async Task IsAvailable_RequiresALiveSession_NotJustTheFlag()
    {
        // Arrange
        // The flag alone kept an agent who closed the browser "available" for routed SMS until the five-minute
        // pickup sweep re-pooled each thread. Every message pushed at them in that window sat unanswered.
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var harness = new AvailabilityHarness(agent, sessionHeartbeatUtc: null);

        await harness.Service.SetAvailableAsync(agent, available: true, TestContext.Current.CancellationToken);

        // Act
        var available = await harness.Service.IsAvailableAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(available);
    }

    [Fact]
    public async Task IsAvailable_RequiresTheFlag_NotJustALiveSession()
    {
        // Arrange
        // An agent signed in for voice has not thereby volunteered for SMS; the flag is a separate choice.
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var harness = new AvailabilityHarness(agent, sessionHeartbeatUtc: AvailabilityHarness.Now);

        await harness.Service.SetAvailableAsync(agent, available: false, TestContext.Current.CancellationToken);

        // Act
        var available = await harness.Service.IsAvailableAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(available);
    }

    [Fact]
    public async Task IsAvailable_WithTheFlagAndAFreshHeartbeat_IsTrue()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var harness = new AvailabilityHarness(agent, sessionHeartbeatUtc: AvailabilityHarness.Now.AddSeconds(-5));

        await harness.Service.SetAvailableAsync(agent, available: true, TestContext.Current.CancellationToken);

        // Act
        var available = await harness.Service.IsAvailableAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(available);
    }

    [Fact]
    public async Task IsAvailable_WithAStaleHeartbeat_IsFalse()
    {
        // Arrange
        // The heartbeat timeout is the same one voice presence uses, so an agent is not live for one channel and
        // gone for the other.
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var harness = new AvailabilityHarness(agent, sessionHeartbeatUtc: AvailabilityHarness.Now.AddMinutes(-10));

        await harness.Service.SetAvailableAsync(agent, available: true, TestContext.Current.CancellationToken);

        // Act
        var available = await harness.Service.IsAvailableAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(available);
    }

    private sealed class AvailabilityHarness
    {
        public static readonly DateTime Now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

        public SmsAgentAvailabilityService Service { get; }

        public AvailabilityHarness(AgentProfile agent, DateTime? sessionHeartbeatUtc)
        {
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<AgentProfile>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var sessionManager = new Mock<IAgentSessionManager>();
            sessionManager
                .Setup(manager => manager.FindByUserIdAsync(agent.UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessionHeartbeatUtc is null
                    ? null
                    : new AgentSession
                    {
                        ItemId = "s1",
                        UserId = agent.UserId,
                        IsOnline = true,
                        LastHeartbeatUtc = sessionHeartbeatUtc,
                    });

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(Now);

            Service = new SmsAgentAvailabilityService(
                agentManager.Object,
                sessionManager.Object,
                new OptionsWrapper<AgentAvailabilityOptions>(new AgentAvailabilityOptions()),
                clock.Object);
        }
    }

}
