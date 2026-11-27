using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Mail the address's sending limits held back, on a real SQLite store: an acknowledgement that would arrive after the
/// conversation was answered is not sent, and held mail can be brought forward to ask for new turns once the limits change.
/// </summary>
public sealed class MessagingOutboxHeldMailTests : IAsyncLifetime
{
    private const string Channel = "Email";
    private const string ServiceAddress = "support@contoso.com";
    private const string ConversationId = "conversation-1";

    private static readonly DateTime _now = new(2026, 11, 27, 15, 30, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"outbox-held-{Guid.NewGuid():N}.db");
    private readonly Mock<IMessagingChannel> _channel = new();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new OmnichannelMessageIndexProvider(), new MessagingOutboxIndexProvider()], OmnichannelConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await schemaBuilder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
                .Column<string>("Channel", column => column.WithLength(50))
                .Column<string>("CustomerAddress", column => column.WithLength(255))
                .Column<string>("ServiceAddress", column => column.WithLength(255))
                .Column<DateTime>("CreatedUtc", column => column.NotNull())
                .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
                .Column<string>("ConversationId", column => column.WithLength(26))
                .Column<string>("ProviderMessageId", column => column.WithLength(128)),
                collection: OmnichannelConstants.CollectionName);

            await schemaBuilder.CreateMapIndexTableAsync<MessagingOutboxIndex>(table => table
                .Column<string>("Channel", column => column.WithLength(50))
                .Column<string>("ServiceAddress", column => column.WithLength(255))
                .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
                collection: OmnichannelConstants.CollectionName);

            await transaction.CommitAsync(cancellationToken);
        }

        _channel.SetupGet(channel => channel.Name).Returns(Channel);
        _channel.SetupGet(channel => channel.Capabilities).Returns(new MessagingChannelCapabilities());
        _channel
            .Setup(channel => channel.SendAsync(It.IsAny<MessagingOutboundMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MessageDispatchResult.Success("provider-1"));
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DispatchDueAsync_AHeldAcknowledgement_IsNotSentOnceAPersonAnswered()
    {
        // Arrange: "we got your message" was held at the limit, and an agent replied before its turn came.
        var acknowledgement = Queued("ack", MessagingOutboundPurpose.AutoReply, _now.AddMinutes(-10), nextAttemptUtc: _now.AddMinutes(-1));
        await SaveAsync(acknowledgement, Sent("agent-reply", _now.AddMinutes(-5)));

        // Act
        var accepted = await DispatchAsync();

        // Assert
        Assert.Equal(0, accepted);
        _channel.Verify(channel => channel.SendAsync(It.IsAny<MessagingOutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);

        var stored = await LoadAsync("ack");
        Assert.Equal(MessageDeliveryStatus.Failed.ToString(), stored.DeliveryStatus);
        Assert.Null(stored.GetOrCreate<OutboundDeliveryState>().NextAttemptUtc);
    }

    [Fact]
    public async Task DispatchDueAsync_AHeldAcknowledgement_IsSentWhileTheConversationWaitsForAnAnswer()
    {
        // Arrange: only a broadcast went out after it, which answers nothing.
        var broadcast = Sent("broadcast", _now.AddMinutes(-5));
        broadcast.Put(new MessagingBroadcastLink { BroadcastId = "broadcast-1" });

        await SaveAsync(Queued("ack", MessagingOutboundPurpose.AutoReply, _now.AddMinutes(-10), nextAttemptUtc: _now.AddMinutes(-1)), broadcast);

        // Act
        var accepted = await DispatchAsync();

        // Assert
        Assert.Equal(1, accepted);
        Assert.Equal(MessageDeliveryStatus.Sent.ToString(), (await LoadAsync("ack")).DeliveryStatus);
    }

    [Fact]
    public async Task BringForwardAsync_MakesOnlyThatEndpointsHeldMailDueNow()
    {
        // Arrange: three emails held under a low limit, and one on another address.
        await SaveAsync(
            Queued("second", MessagingOutboundPurpose.Broadcast, _now.AddMinutes(-20), nextAttemptUtc: _now.AddHours(2)),
            Queued("first", MessagingOutboundPurpose.Broadcast, _now.AddMinutes(-20), nextAttemptUtc: _now.AddHours(1)),
            Queued("third", MessagingOutboundPurpose.Broadcast, _now.AddMinutes(-20), nextAttemptUtc: _now.AddHours(3)),
            Queued("other", MessagingOutboundPurpose.Broadcast, _now.AddMinutes(-20), nextAttemptUtc: _now.AddHours(1), serviceAddress: "sales@contoso.com"));

        // Act
        int moved;

        await using (var session = _store.CreateSession())
        {
            moved = await CreateOutbox(session).BringForwardAsync(Channel, ServiceAddress, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(3, moved);

        var first = (await LoadAsync("first")).GetOrCreate<OutboundDeliveryState>().NextAttemptUtc;
        var second = (await LoadAsync("second")).GetOrCreate<OutboundDeliveryState>().NextAttemptUtc;
        var third = (await LoadAsync("third")).GetOrCreate<OutboundDeliveryState>().NextAttemptUtc;

        Assert.All(new[] { first, second, third }, nextAttemptUtc => Assert.Equal(_now, nextAttemptUtc));
        Assert.Equal(_now.AddHours(1), (await LoadAsync("other")).GetOrCreate<OutboundDeliveryState>().NextAttemptUtc);
    }

    private async Task<int> DispatchAsync()
    {
        await using var session = _store.CreateSession();

        var accepted = await CreateOutbox(session).DispatchDueAsync(TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return accepted;
    }

    private MessagingOutbox CreateOutbox(ISession session)
    {
        var resolver = new Mock<IMessagingChannelResolver>();
        resolver.Setup(value => value.GetAll()).Returns([_channel.Object]);
        resolver.Setup(value => value.Get(Channel)).Returns(_channel.Object);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new MessagingOutbox(
            resolver.Object,
            Mock.Of<IMessagingRealTimeNotifier>(),
            Mock.Of<IMessagingConversationStore>(),
            new FakeAttachmentUrlProvider(),
            Mock.Of<IDocumentManager<MessagingBroadcastQueueOutcomes>>(),
            new OptionsWrapper<MessagingWorkspaceOptions>(new MessagingWorkspaceOptions()),
            session,
            clock.Object,
            NullLogger<MessagingOutbox>.Instance);
    }

    private async Task SaveAsync(params OmnichannelMessage[] messages)
    {
        await using var session = _store.CreateSession();

        foreach (var message in messages)
        {
            await session.SaveAsync(message, false, OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<OmnichannelMessage> LoadAsync(string id)
    {
        await using var session = _store.CreateSession();

        var messages = await session.Query<OmnichannelMessage, OmnichannelMessageIndex>(collection: OmnichannelConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken);

        return messages.Single(message => message.Id == id);
    }

    private static OmnichannelMessage Queued(string id, MessagingOutboundPurpose purpose, DateTime createdUtc, DateTime nextAttemptUtc, string serviceAddress = ServiceAddress)
    {
        var message = Outbound(id, createdUtc, serviceAddress);
        message.DeliveryStatus = MessageDeliveryStatus.Queued.ToString();
        message.Put(new OutboundDeliveryState { Purpose = purpose, NextAttemptUtc = nextAttemptUtc });

        return message;
    }

    private static OmnichannelMessage Sent(string id, DateTime createdUtc)
    {
        var message = Outbound(id, createdUtc, ServiceAddress);
        message.DeliveryStatus = MessageDeliveryStatus.Sent.ToString();

        return message;
    }

    private static OmnichannelMessage Outbound(string id, DateTime createdUtc, string serviceAddress)
        => new()
        {
            Id = id,
            Channel = Channel,
            ServiceAddress = serviceAddress,
            CustomerAddress = "lena@example.com",
            ConversationId = ConversationId,
            Content = id,
            CreatedUtc = createdUtc,
            IsInbound = false,
        };
}
