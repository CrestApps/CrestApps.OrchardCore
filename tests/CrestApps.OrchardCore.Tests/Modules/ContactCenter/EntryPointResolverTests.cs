using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class EntryPointResolverTests
{
    [Fact]
    public void CreatePlan_WhenOpen_QueuesToTarget()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "e1", TargetQueueId = "q1", Priority = InteractionPriority.High };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.Equal("q1", plan.TargetQueueId);
        Assert.Equal(InteractionPriority.High, plan.Priority);
    }

    [Fact]
    public void CreatePlan_WhenClosedWithOverflow_QueuesToOverflow()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetQueueId = "q1",
            OverflowQueueId = "q2",
            ClosedAction = EntryPointClosedAction.Overflow,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.Equal("q2", plan.TargetQueueId);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.Voicemail)]
    [InlineData(EntryPointClosedAction.Reject)]
    public void CreatePlan_WhenClosedWithVoicemailOrReject_DoesNotQueue(EntryPointClosedAction action)
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "e1", TargetQueueId = "q1", ClosedAction = action };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.False(plan.ShouldQueue);
        Assert.Null(plan.TargetQueueId);
    }

    [Fact]
    public void CreatePlan_WhenOpenWithAgentTarget_RoutesDirectlyToAgentWithNoQueueFallback()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            TargetQueueId = "q1",
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.True(plan.RouteToAgent);
        Assert.Equal("agent1", plan.TargetAgentId);

        // The call is carried under the synthetic direct-routing queue, never the (now unused) TargetQueueId.
        Assert.Equal(ContactCenterConstants.DirectRouting.QueueId, plan.TargetQueueId);
    }

    [Theory]
    [InlineData(45, 45)]   // configured window is honored
    [InlineData(0, 30)]    // non-positive window falls back to the default
    [InlineData(1000, 300)] // above the maximum is clamped
    [InlineData(1, 5)]     // below the minimum is clamped up
    public void CreatePlan_WhenAgentTargetWithVoicemail_UsesConfiguredRingWindow(int configured, int expected)
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            VoicemailEnabled = true,
            RingTimeoutSeconds = configured,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.RouteToAgent);
        Assert.Equal(expected, plan.RingTimeoutSeconds);
    }

    [Fact]
    public void CreatePlan_WhenAgentTargetWithVoicemailDisabled_UsesZeroRingWindow()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            VoicemailEnabled = false,
            RingTimeoutSeconds = 45,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert: 0 signals "no voicemail" downstream — the caller keeps ringing and is held for the agent.
        Assert.True(plan.RouteToAgent);
        Assert.Equal(0, plan.RingTimeoutSeconds);
    }

    [Fact]
    public void CreatePlan_WhenAgentTargetHasNoAgent_FallsBackToQueueRouting()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = null,
            TargetQueueId = "q1",
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.False(plan.RouteToAgent);
        Assert.Equal("q1", plan.TargetQueueId);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.HoldInQueue)]
    [InlineData(EntryPointClosedAction.Overflow)]
    [InlineData(EntryPointClosedAction.Voicemail)]
    public void CreatePlan_WhenClosedWithAgentTarget_SendsToVoicemail(EntryPointClosedAction action)
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            ClosedAction = action,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.False(plan.ShouldQueue);
        Assert.Null(plan.TargetQueueId);
        Assert.Equal(EntryPointClosedAction.Voicemail, plan.ClosedAction);
    }

    [Fact]
    public void CreatePlan_WhenClosedWithAgentTargetAndReject_Rejects()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            ClosedAction = EntryPointClosedAction.Reject,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.False(plan.ShouldQueue);
        Assert.Null(plan.TargetQueueId);
        Assert.Equal(EntryPointClosedAction.Reject, plan.ClosedAction);
    }

    [Fact]
    public async Task ResolveAsync_MatchesDialedNumberAndEvaluatesBusinessHours()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetQueueId = "q1",
            BusinessHoursCalendarId = "cal1",
            DialedNumbers = ["+15551234567"],
        };

        var manager = new Mock<IContactCenterEntryPointManager>();
        manager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([entryPoint]);

        var businessHours = new Mock<IBusinessHoursService>();
        businessHours.Setup(b => b.IsOpenAsync("cal1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var resolver = CreateResolver(manager.Object, businessHours.Object);

        // Act
        var plan = await resolver.ResolveAsync("+15551234567", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.IsOpen);
        Assert.Equal("q1", plan.TargetQueueId);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoEntryPointMatches_ReturnsNull()
    {
        // Arrange
        var manager = new Mock<IContactCenterEntryPointManager>();
        manager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var resolver = CreateResolver(manager.Object, new Mock<IBusinessHoursService>().Object);

        // Act
        var plan = await resolver.ResolveAsync("+15550000000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(plan);
    }

    // The number a call was dialed to is an address; the call entry point that picked it answers, whatever its typed
    // numbers say, and an SMS entry point that picked the same number does not.
    [Fact]
    public async Task FindByDialedNumberAsync_ReturnsTheCallEntryPointThatPickedTheAddress()
    {
        // Arrange
        var address = new OmnichannelChannelEndpoint
        {
            ItemId = "line",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms],
            Value = "+15551234567",
        };
        var texts = new ContactCenterEntryPoint { ItemId = "texts", Channel = OmnichannelConstants.Channels.Sms, AddressIds = ["line"], Enabled = true };
        var calls = new ContactCenterEntryPoint { ItemId = "calls", Channel = OmnichannelConstants.Channels.Phone, AddressIds = ["line"], Enabled = true };

        var manager = new Mock<IContactCenterEntryPointManager>();
        manager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([texts, calls]);

        var resolver = CreateResolver(manager.Object, new Mock<IBusinessHoursService>().Object, address);

        // Act
        var entryPoint = await resolver.FindByDialedNumberAsync("+15551234567", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("calls", entryPoint?.ItemId);
    }

    // Typed numbers were compared as exact text, so a number typed in national format never matched its calls.
    [Fact]
    public async Task FindByDialedNumberAsync_MatchesATypedNumberWrittenInAnotherForm()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "legacy", DialedNumbers = ["(555) 123-4567"], Enabled = true };

        var manager = new Mock<IContactCenterEntryPointManager>();
        manager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([entryPoint]);

        var resolver = CreateResolver(manager.Object, new Mock<IBusinessHoursService>().Object);

        // Act
        var resolved = await resolver.FindByDialedNumberAsync("+15551234567", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("legacy", resolved?.ItemId);
    }

    private static EntryPointResolver CreateResolver(
        IContactCenterEntryPointManager manager,
        IBusinessHoursService businessHours,
        params OmnichannelChannelEndpoint[] addresses)
    {
        var addressManager = new Mock<IOmnichannelChannelEndpointManager>();
        addressManager
            .Setup(m => m.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string channel, string value, CancellationToken _) =>
                ValueTask.FromResult(addresses.FirstOrDefault(address => address.Value == value && address.HasCapability(channel))));

        var phoneNumbers = new Mock<IPhoneNumberService>();
        phoneNumbers
            .Setup(service => service.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns(new TryFormatToE164Callback((string raw, string region, out string e164) =>
            {
                e164 = raw == "(555) 123-4567" ? "+15551234567" : raw;

                return raw.StartsWith('+') || raw == "(555) 123-4567";
            }));

        return new EntryPointResolver(manager, addressManager.Object, phoneNumbers.Object, businessHours);
    }

    private delegate bool TryFormatToE164Callback(string rawNumber, string regionCode, out string e164Number);
}
