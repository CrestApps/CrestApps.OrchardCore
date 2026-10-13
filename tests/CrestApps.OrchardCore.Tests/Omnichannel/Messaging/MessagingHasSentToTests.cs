using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A workflow that texts a customer after a missed call can keep to a first text: it asks whether the customer was ever
/// texted before. These run that question against a real SQLite database, since the answer is the query itself.
/// </summary>
public sealed class MessagingHasSentToTests
{
    private const string CustomerAddress = "+15551112222";

    private static readonly DateTime _start = new(2026, 10, 13, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task IsFalse_WhenTheCustomerWasNeverTexted()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Message("+15559990000", isInbound: false, MessageDeliveryStatus.Delivered));

        // Act
        var hasSent = await harness.Service.HasSentToAsync(OmnichannelConstants.Channels.Sms, CustomerAddress, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(hasSent);
    }

    [Fact]
    public async Task IsTrue_WhenTheCustomerWasTextedBefore_EvenWithSpacesInTheNumber()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Message(CustomerAddress, isInbound: false, MessageDeliveryStatus.Delivered));

        // Act
        var hasSent = await harness.Service.HasSentToAsync(OmnichannelConstants.Channels.Sms, "+1 555 111 2222", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(hasSent);
    }

    [Fact]
    public async Task IsTrue_ForAnAutomatedText_ThatNoWorkspaceThreadHolds()
    {
        // Arrange
        // An automated (AI) conversation's texts carry no delivery status and are not linked to a workspace thread
        // until a person takes over; the customer still received them.
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Message(CustomerAddress, isInbound: false, deliveryStatus: null));

        // Act
        var hasSent = await harness.Service.HasSentToAsync(OmnichannelConstants.Channels.Sms, CustomerAddress, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(hasSent);
    }

    [Fact]
    public async Task IsFalse_WhenTheCustomerOnlyTextedUs()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Message(CustomerAddress, isInbound: true, deliveryStatus: null));

        // Act
        var hasSent = await harness.Service.HasSentToAsync(OmnichannelConstants.Channels.Sms, CustomerAddress, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(hasSent);
    }

    [Fact]
    public async Task IsFalse_WhenEveryTextToTheCustomerFailedOrWasNotDelivered()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(
            Message(CustomerAddress, isInbound: false, MessageDeliveryStatus.Failed),
            Message(CustomerAddress, isInbound: false, MessageDeliveryStatus.Undelivered));

        // Act
        var hasSent = await harness.Service.HasSentToAsync(OmnichannelConstants.Channels.Sms, CustomerAddress, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(hasSent);
    }

    [Fact]
    public async Task IsFalse_WhenTheCustomerWasOnlyReachedOnAnotherChannel()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var message = Message(CustomerAddress, isInbound: false, MessageDeliveryStatus.Delivered);
        message.Channel = "WhatsApp";
        await harness.SeedAsync(message);

        // Act
        var hasSent = await harness.Service.HasSentToAsync(OmnichannelConstants.Channels.Sms, CustomerAddress, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(hasSent);
    }

    private static OmnichannelMessage Message(string customerAddress, bool isInbound, MessageDeliveryStatus? deliveryStatus)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Channel = OmnichannelConstants.Channels.Sms,
            CustomerAddress = customerAddress,
            ServiceAddress = "+15553334444",
            Content = "Hello",
            CreatedUtc = _start,
            IsInbound = isInbound,
            DeliveryStatus = deliveryStatus?.ToString(),
        };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly IStore _store;
        private readonly ISession _session;
        private readonly string _databasePath;

        private Harness(IStore store, string databasePath)
        {
            _store = store;
            _databasePath = databasePath;
            _session = store.CreateSession();

            Service = new MessagingConversationService(
                Mock.Of<IMessagingConversationStore>(),
                MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object),
                Mock.Of<IContentManager>(),
                Mock.Of<IMessagingContactResolver>(),
                Mock.Of<IMessagingRealTimeNotifier>(),
                Mock.Of<IAuthorizationService>(),
                _session,
                new NoOpSmsFirstResponseSlaService(),
                new FakeAttachmentUrlProvider(),
                Mock.Of<IClock>(),
                NullLogger<MessagingConversationService>.Instance);
        }

        public MessagingConversationService Service { get; }

        public static async Task<Harness> CreateAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var databasePath = Path.Combine(Path.GetTempPath(), $"messaging-has-sent-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

            store.RegisterIndexes([new OmnichannelMessageIndexProvider()], OmnichannelConstants.CollectionName);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                await schemaBuilder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
                    .Column<string>("Channel", column => column.WithLength(50))
                    .Column<string>("CustomerAddress", column => column.WithLength(255))
                    .Column<string>("ServiceAddress", column => column.WithLength(255))
                    .Column<DateTime>("CreatedUtc", column => column.NotNull())
                    .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
                    .Column<string>("ConversationId", column => column.WithLength(26))
                    .Column<string>("ProviderMessageId", column => column.WithLength(128)),
                    collection: OmnichannelConstants.CollectionName);

                await transaction.CommitAsync(cancellationToken);
            }

            return new Harness(store, databasePath);
        }

        public async Task SeedAsync(params OmnichannelMessage[] messages)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using (var session = _store.CreateSession())
            {
                foreach (var message in messages)
                {
                    await session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
                }

                await session.SaveChangesAsync(cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _session.DisposeAsync();
            TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);
        }
    }
}
