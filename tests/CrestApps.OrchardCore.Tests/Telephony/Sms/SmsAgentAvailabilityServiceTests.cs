using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
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

            Service = new SmsAgentAvailabilityService(AgentManager.Object, clock.Object);
        }
    }
}
