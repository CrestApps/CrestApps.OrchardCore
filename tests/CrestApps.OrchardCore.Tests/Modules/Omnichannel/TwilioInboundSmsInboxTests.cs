using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Twilio;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// The Twilio inbound-SMS webhook stores a text in the durable provider webhook inbox before it answers Twilio.
/// Bug: the text was processed in a discarded background task and de-duplicated in memory, so a restart lost it and a
/// redelivery that reached another node was answered twice.
/// </summary>
public sealed class TwilioInboundSmsInboxTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptInboundAsync_ARedeliveredMessageSid_IsStoredOnceAndProcessedOnce()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"twilio-sms-inbox-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);
        var handler = new RecordingHandler();

        try
        {
            var distributedLock = CreateDistributedLock();

            // Act
            // Two deliveries of one text, each in its own request and session, as on two nodes.
            IResult first;
            IResult second;

            await using (var session = store.CreateSession())
            {
                first = await AcceptAsync(CreateInbox(session, distributedLock, []), "SM-1");
            }

            await using (var session = store.CreateSession())
            {
                second = await AcceptAsync(CreateInbox(session, distributedLock, []), "SM-1");
            }

            // Nothing has processed the text yet; a node that stopped here would leave it in storage.
            int pendingBeforeDispatch;
            ProviderWebhookInboxMessage stored;

            await using (var session = store.CreateSession())
            {
                stored = await new ProviderWebhookInboxStore(session).FindByDeliveryAsync("Twilio", "SM-1", TestContext.Current.CancellationToken);
                pendingBeforeDispatch = handler.Payloads.Count;
            }

            await using (var session = store.CreateSession())
            {
                await CreateInbox(session, distributedLock, [handler]).DispatchDueAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                await CreateInbox(session, distributedLock, [handler]).DispatchDueAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.IsType<Ok>(first);
            Assert.IsType<Ok>(second);
            Assert.NotNull(stored);
            Assert.Equal(ProviderWebhookInboxStatus.Pending, stored.Status);
            Assert.Equal(OmnichannelConstants.WebhookInboxHandlers.SmsReceived, stored.HandlerName);
            Assert.Equal(0, pendingBeforeDispatch);

            var payload = Assert.Single(handler.Payloads);
            var message = JsonSerializer.Deserialize<OmnichannelMessage>(payload);
            Assert.Equal("SM-1", message.ProviderMessageId);
            Assert.Equal("+15550001111", message.CustomerAddress);
            Assert.Equal("Is my appointment still on?", message.Content);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task AcceptInboundAsync_WhenTheInboxCannotStoreTheText_AsksTwilioToSendItAgain()
    {
        // Arrange
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQLite Error 5: 'database is locked'."));

        // Act
        var result = await AcceptAsync(inbox.Object, "SM-1");

        // Assert
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task AcceptInboundAsync_WhenTheInboxIsBusy_AsksTwilioToSendItAgain()
    {
        // Arrange
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Busy,
            });

        // Act
        var result = await AcceptAsync(inbox.Object, "SM-1");

        // Assert
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        inbox.Verify(service => service.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Task<IResult> AcceptAsync(IProviderWebhookInbox inbox, string messageSid)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(inbox)
                .BuildServiceProvider(),
        };

        var message = new OmnichannelMessage
        {
            CustomerAddress = "+15550001111",
            ServiceAddress = "+18880002222",
            Content = "Is my appointment still on?",
            Channel = OmnichannelConstants.Channels.Sms,
            CreatedUtc = _now,
            IsInbound = true,
            ProviderMessageId = messageSid,
        };

        return TwilioWebhookEndpoint.AcceptInboundAsync(
            context,
            message,
            new Mock<IShellHost>().Object,
            new ShellSettings { Name = "Default" },
            NullLogger.Instance);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ProviderWebhookInboxMessageIndexProvider(new ProviderIdentityResolver([]))]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime?>("ProcessedUtc"),
            collection: ContactCenterStorage.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static ProviderWebhookInbox CreateInbox(
        ISession session,
        IDistributedLock distributedLock,
        IEnumerable<IProviderWebhookInboxHandler> handlers)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        ProviderWebhookInbox inbox = null;
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(service => service.ExecuteAsync(It.IsAny<Func<IProviderWebhookInbox, Task>>()))
            .Returns((Func<IProviderWebhookInbox, Task> operation) => operation(inbox));

        inbox = new ProviderWebhookInbox(
            handlers,
            new ProviderWebhookInboxStore(session),
            session,
            distributedLock,
            new ProviderIdentityResolver([]),
            scopeExecutor.Object,
            clock.Object,
            Options.Create(new ContactCenterRetentionOptions()),
            NullLogger<ProviderWebhookInbox>.Instance);

        return inbox;
    }

    private static IDistributedLock CreateDistributedLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return distributedLock.Object;
    }

    private sealed class RecordingHandler : IProviderWebhookInboxHandler
    {
        public List<string> Payloads { get; } = [];

        public string TechnicalName => OmnichannelConstants.WebhookInboxHandlers.SmsReceived;

        public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

        public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);

            return Task.CompletedTask;
        }
    }
}
