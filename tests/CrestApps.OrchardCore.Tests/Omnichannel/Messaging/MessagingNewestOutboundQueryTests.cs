using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Bug: a delivery receipt without a matching provider id loaded every outbound message of the thread to pick the
/// newest. The newest is now picked by the database in one ordered query.
/// </summary>
public sealed class MessagingNewestOutboundQueryTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FindNewestOutboundAsync_PicksTheNewestOutbound_AndCanSkipThoseWithAProviderId()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"messaging-newest-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new MessageIndexProvider()], OmnichannelConstants.CollectionName);

        try
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);

            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

                await new SchemaBuilder(store.Configuration, transaction).CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
                    .Column<string>("Channel", column => column.WithLength(50))
                    .Column<string>("CustomerAddress", column => column.WithLength(255))
                    .Column<string>("ServiceAddress", column => column.WithLength(255))
                    .Column<DateTime>("CreatedUtc", column => column.NotNull())
                    .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
                    .Column<string>("ConversationId", column => column.WithLength(26))
                    .Column<string>("ProviderMessageId", column => column.WithLength(128)),
                    collection: OmnichannelConstants.CollectionName);

                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                await SaveAsync(session, "older-no-id", isInbound: false, providerMessageId: null, _now.AddMinutes(-10));
                await SaveAsync(session, "newer-with-id", isInbound: false, providerMessageId: "SM-1", _now.AddMinutes(-5));
                await SaveAsync(session, "newest-inbound", isInbound: true, providerMessageId: null, _now.AddMinutes(-1));
                await SaveAsync(session, "other-thread", isInbound: false, providerMessageId: null, _now, conversationId: "c2");
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            OmnichannelMessage newest;
            OmnichannelMessage newestWithoutId;

            await using (var session = store.CreateSession())
            {
                newest = await MessagingConversationService.FindNewestOutboundAsync(session, "c1", withoutProviderMessageId: false, TestContext.Current.CancellationToken);
                newestWithoutId = await MessagingConversationService.FindNewestOutboundAsync(session, "c1", withoutProviderMessageId: true, TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Equal("newer-with-id", newest.Content);
            Assert.Equal("older-no-id", newestWithoutId.Content);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static Task SaveAsync(ISession session, string content, bool isInbound, string providerMessageId, DateTime createdUtc, string conversationId = "c1")
        => session.SaveAsync(
            new OmnichannelMessage
            {
                Channel = OmnichannelConstants.Channels.Sms,
                Content = content,
                IsInbound = isInbound,
                ProviderMessageId = providerMessageId,
                CreatedUtc = createdUtc,
                ConversationId = conversationId,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

    private sealed class MessageIndexProvider : IndexProvider<OmnichannelMessage>
    {
        public MessageIndexProvider()
        {
            CollectionName = OmnichannelConstants.CollectionName;
        }

        public override void Describe(DescribeContext<OmnichannelMessage> context)
        {
            context
                .For<OmnichannelMessageIndex>()
                .Map(message => new OmnichannelMessageIndex
                {
                    Channel = message.Channel,
                    CreatedUtc = message.CreatedUtc,
                    IsInbound = message.IsInbound,
                    ConversationId = message.ConversationId,
                    ProviderMessageId = message.ProviderMessageId,
                });
        }
    }
}
