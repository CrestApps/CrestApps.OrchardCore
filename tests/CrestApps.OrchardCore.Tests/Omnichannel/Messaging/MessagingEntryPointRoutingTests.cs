using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Where texts to a number go used to be saved on the number. It is now set on the inbound entry point that answers the
/// number for the channel, with opening hours and an auto-reply for when it is closed, and the upgrade moves each
/// number's routing onto an entry point.
/// </summary>
public sealed class MessagingEntryPointRoutingTests
{
    [Fact]
    public async Task Resolve_UsesTheEnabledEntryPointThatAnswersTheNumberOnTheChannel()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms);
        var calls = EntryPoint("calls", OmnichannelConstants.Channels.Phone, "line");
        calls.TargetQueueId = "phones";
        var disabled = EntryPoint("old", OmnichannelConstants.Channels.Sms, "line");
        disabled.Enabled = false;
        disabled.TargetQueueId = "old-queue";
        var texts = EntryPoint("texts", OmnichannelConstants.Channels.Sms, "line");
        texts.TargetQueueId = "support";
        texts.Put(new MessagingEntryPointSettings { DistributionMode = ConversationDistributionMode.Routed, AutoReplyMessage = " Thanks! " });

        var resolver = CreateResolver([calls, disabled, texts], isOpen: true);

        // Act
        var routing = await resolver.ResolveAsync(line, OmnichannelConstants.Channels.Sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("texts", routing.EntryPointId);
        Assert.Equal(ConversationRouteTargetType.Queue, routing.TargetType);
        Assert.Equal("support", routing.TargetId);
        Assert.Equal(ConversationDistributionMode.Routed, routing.DistributionMode);
        Assert.Equal("Thanks!", routing.AutoReplyMessage);
    }

    [Fact]
    public async Task Resolve_RoutesAnAgentEntryPointToTheAgent()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        var texts = EntryPoint("texts", OmnichannelConstants.Channels.Sms, "line");
        texts.TargetType = EntryPointTargetType.Agent;
        texts.TargetAgentId = "agent-1";
        texts.TargetQueueId = null;

        var resolver = CreateResolver([texts], isOpen: true);

        // Act
        var routing = await resolver.ResolveAsync(line, OmnichannelConstants.Channels.Sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ConversationRouteTargetType.Agent, routing.TargetType);
        Assert.Equal("agent-1", routing.TargetId);
    }

    [Fact]
    public async Task Resolve_ReturnsNothingWhenNoEntryPointAnswersTheNumberForTexts()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms);
        var resolver = CreateResolver([EntryPoint("calls", OmnichannelConstants.Channels.Phone, "line")], isOpen: true);

        // Act
        var routing = await resolver.ResolveAsync(line, OmnichannelConstants.Channels.Sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(routing);
    }

    [Fact]
    public async Task Resolve_SendsTheClosedAutoReplyWhileClosed()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        var texts = EntryPoint("texts", OmnichannelConstants.Channels.Sms, "line");
        texts.BusinessHoursCalendarId = "office";
        texts.Put(new MessagingEntryPointSettings { AutoReplyMessage = "Thanks!", ClosedAutoReplyMessage = "We are closed." });

        var resolver = CreateResolver([texts], isOpen: false);

        // Act
        var routing = await resolver.ResolveAsync(line, OmnichannelConstants.Channels.Sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(routing.IsOpen);
        Assert.Equal("We are closed.", routing.AutoReplyMessage);

        // A closed entry point still routes: the conversation waits in the queue for when it opens.
        Assert.Equal("queue", routing.TargetId);
    }

    [Fact]
    public async Task Resolve_SendsTheOrdinaryAutoReplyWhileClosedWhenThereIsNoClosedOne()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        var texts = EntryPoint("texts", OmnichannelConstants.Channels.Sms, "line");
        texts.BusinessHoursCalendarId = "office";
        texts.Put(new MessagingEntryPointSettings { AutoReplyMessage = "Thanks!" });

        var resolver = CreateResolver([texts], isOpen: false);

        // Act
        var routing = await resolver.ResolveAsync(line, OmnichannelConstants.Channels.Sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Thanks!", routing.AutoReplyMessage);
    }

    [Fact]
    public async Task Resolve_FindsTheEntryPointThroughAnIdTheAddressAbsorbedInAMerge()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        line.MergedItemIds = ["old-sms-record"];
        var texts = EntryPoint("texts", OmnichannelConstants.Channels.Sms, "old-sms-record");

        var resolver = CreateResolver([texts], isOpen: true);

        // Act
        var routing = await resolver.ResolveAsync(line, OmnichannelConstants.Channels.Sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("texts", routing.EntryPointId);
    }

    [Fact]
    public async Task Migration_MovesANumbersRoutingOntoATextEntryPointNamedAfterIt()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms);
        line.DisplayText = "Main line";
        line.Put(new MessagingEndpointRoutingSettings
        {
            TargetType = ConversationRouteTargetType.Queue,
            TargetId = "support",
            DistributionMode = ConversationDistributionMode.Routed,
            AutoReplyMessage = "Thanks!",
        });

        // The number's call entry point already carries its name.
        var calls = EntryPoint("calls", OmnichannelConstants.Channels.Phone, "line");
        calls.Name = "Main line";
        var harness = new MigrationHarness([line], [calls]);

        // Act
        await harness.RunAsync();

        // Assert
        var texts = Assert.Single(harness.EntryPoints, entryPoint => entryPoint.Channel == OmnichannelConstants.Channels.Sms);
        Assert.Equal("Main line (SMS)", texts.Name);
        Assert.Equal(["line"], texts.AddressIds);
        Assert.Equal(EntryPointTargetType.Queue, texts.TargetType);
        Assert.Equal("support", texts.TargetQueueId);
        Assert.True(texts.Enabled);

        Assert.True(texts.TryGet<MessagingEntryPointSettings>(out var settings));
        Assert.Equal(ConversationDistributionMode.Routed, settings.DistributionMode);
        Assert.Equal("Thanks!", settings.AutoReplyMessage);

        Assert.False(line.Properties.ContainsKey(nameof(MessagingEndpointRoutingSettings)));
        harness.AddressManager.Verify(manager => manager.UpdateAsync(line, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Migration_MovesRoutingTheSmsPortalStoredUnderItsOwnName()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        line.Properties = new Dictionary<string, object>
        {
            ["SmsEndpointRoutingSettings"] = System.Text.Json.JsonSerializer.SerializeToElement(new { TargetType = "Queue", TargetId = "support", DistributionMode = "SharedPool" }),
        };
        var harness = new MigrationHarness([line], []);

        // Act
        await harness.RunAsync();

        // Assert
        var texts = Assert.Single(harness.EntryPoints);
        Assert.Equal(EntryPointTargetType.Queue, texts.TargetType);
        Assert.Equal("support", texts.TargetQueueId);
        Assert.False(line.Properties.ContainsKey("SmsEndpointRoutingSettings"));
    }

    [Fact]
    public async Task Migration_MovesAnAgentRoute()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        line.Put(new MessagingEndpointRoutingSettings { TargetType = ConversationRouteTargetType.Agent, TargetId = "agent-1" });
        var harness = new MigrationHarness([line], []);

        // Act
        await harness.RunAsync();

        // Assert
        var texts = Assert.Single(harness.EntryPoints);
        Assert.Equal("line", texts.Name);
        Assert.Equal(EntryPointTargetType.Agent, texts.TargetType);
        Assert.Equal("agent-1", texts.TargetAgentId);
        Assert.Null(texts.TargetQueueId);
    }

    [Fact]
    public async Task Migration_CreatesNoEntryPointForANumberThatRoutedNowhere()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        line.Put(new MessagingEndpointRoutingSettings { TargetType = ConversationRouteTargetType.Queue, AutoReplyMessage = "Thanks!" });
        var harness = new MigrationHarness([line], []);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Empty(harness.EntryPoints);
        Assert.False(line.Properties.ContainsKey(nameof(MessagingEndpointRoutingSettings)));
    }

    [Fact]
    public async Task Migration_LeavesANumberAnotherTextEntryPointAlreadyAnswers()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Sms);
        line.Put(new MessagingEndpointRoutingSettings { TargetType = ConversationRouteTargetType.Queue, TargetId = "support" });
        var existing = EntryPoint("texts", OmnichannelConstants.Channels.Sms, "line");
        var harness = new MigrationHarness([line], [existing]);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Same(existing, Assert.Single(harness.EntryPoints));
        Assert.False(line.Properties.ContainsKey(nameof(MessagingEndpointRoutingSettings)));
    }

    [Fact]
    public async Task Migration_DoesNothingWhenNoNumberCarriesRouting()
    {
        // Arrange
        var harness = new MigrationHarness([Address("line", OmnichannelConstants.Channels.Sms)], []);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Empty(harness.EntryPoints);
        harness.AddressManager.Verify(manager => manager.UpdateAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static OmnichannelChannelEndpoint Address(string id, params string[] capabilities)
        => new()
        {
            ItemId = id,
            DisplayText = id,
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [.. capabilities],
            Value = "+15550001234",
        };

    private static ContactCenterEntryPoint EntryPoint(string id, string channel, params string[] addressIds)
        => new()
        {
            ItemId = id,
            Name = id,
            Channel = channel,
            AddressIds = [.. addressIds],
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = "queue",
            Enabled = true,
        };

    private static MessagingInboundRoutingResolver CreateResolver(ContactCenterEntryPoint[] entryPoints, bool isOpen)
    {
        var manager = new Mock<IContactCenterEntryPointManager>();
        manager
            .Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entryPoints.Where(entryPoint => entryPoint.Enabled).ToArray());

        var businessHours = new Mock<IBusinessHoursService>();
        businessHours.Setup(service => service.IsOpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(isOpen);

        return new MessagingInboundRoutingResolver(manager.Object, businessHours.Object);
    }

    private sealed class MigrationHarness
    {
        private readonly Mock<IContactCenterEntryPointManager> _entryPointManager = new();

        public MigrationHarness(OmnichannelChannelEndpoint[] addresses, ContactCenterEntryPoint[] entryPoints)
        {
            EntryPoints = [.. entryPoints];

            AddressManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(addresses);

            _entryPointManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => EntryPoints.ToArray());
            _entryPointManager
                .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ContactCenterEntryPoint { ItemId = "entry-" + EntryPoints.Count });
            _entryPointManager
                .Setup(manager => manager.CreateAsync(It.IsAny<ContactCenterEntryPoint>(), It.IsAny<CancellationToken>()))
                .Returns((ContactCenterEntryPoint entryPoint, CancellationToken _) =>
                {
                    EntryPoints.Add(entryPoint);

                    return ValueTask.CompletedTask;
                });
        }

        public Mock<IOmnichannelChannelEndpointManager> AddressManager { get; } = new();

        public List<ContactCenterEntryPoint> EntryPoints { get; }

        public Task<int> RunAsync()
            => new MessagingEntryPointMigrations(_entryPointManager.Object, AddressManager.Object, NullLogger<MessagingEntryPointMigrations>.Instance)
                .CreateAsync();
    }
}
