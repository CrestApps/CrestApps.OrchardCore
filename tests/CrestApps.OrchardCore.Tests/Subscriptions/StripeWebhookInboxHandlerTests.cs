using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Stripe.Indexes;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Stripe events go through the provider webhook inbox: the endpoint stores a verified event and the inbox runs its
/// payment handlers from storage. A retried dispatch must not run the handlers twice.
/// </summary>
public sealed class StripeWebhookInboxHandlerTests
{
    private const string PaymentIntentSucceeded = """
        {
          "id": "evt_inbox_1",
          "object": "event",
          "type": "payment_intent.succeeded",
          "livemode": false,
          "data": {
            "object": {
              "id": "pi_inbox_1",
              "object": "payment_intent",
              "amount": 2000,
              "currency": "usd",
              "livemode": false,
              "metadata": {}
            }
          }
        }
        """;

    [Fact]
    public async Task HandleAsync_RunsThePaymentHandlersOnce_AndSkipsARetriedDispatch()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"stripe-inbox-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ProcessedStripeWebhookEventIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await CreateSchemaAsync(store);

        var handler = new CountingPaymentEvent();
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));

        try
        {
            // Act
            await using (var session = store.CreateSession())
            {
                await new StripeWebhookInboxHandler([handler], session, clock.Object).HandleAsync(PaymentIntentSucceeded, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                await new StripeWebhookInboxHandler([handler], session, clock.Object).HandleAsync(PaymentIntentSucceeded, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            ProcessedStripeWebhookEvent marker;

            await using (var session = store.CreateSession())
            {
                marker = await session.Query<ProcessedStripeWebhookEvent, ProcessedStripeWebhookEventIndex>(index => index.EventId == "evt_inbox_1")
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Equal(1, handler.SucceededCount);
            Assert.NotNull(marker);
            Assert.Equal("payment_intent.succeeded", marker.EventType);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public void Handler_DeclaresHowItSurvivesARedelivery()
    {
        var handler = new StripeWebhookInboxHandler([], new Mock<ISession>().Object, new Mock<IClock>().Object);

        Assert.Equal(StripeWebhookInboxHandler.HandlerTechnicalName, handler.TechnicalName);
        Assert.Equal(WebhookHandlerReplaySafety.DeduplicatedByEventId, handler.ReplaySafety);
    }

    [Fact]
    public void TryGetInbox_UsesTheInboxOnlyWhenTheStripeHandlerIsRegisteredWithIt()
    {
        // Arrange
        var withoutInbox = new ServiceCollection().BuildServiceProvider();
        var inboxOnly = new ServiceCollection()
            .AddSingleton(new Mock<IProviderWebhookInbox>().Object)
            .BuildServiceProvider();
        var inboxAndHandler = new ServiceCollection()
            .AddSingleton(new Mock<IProviderWebhookInbox>().Object)
            .AddSingleton(new Mock<ISession>().Object)
            .AddSingleton(new Mock<IClock>().Object)
            .AddScoped<IProviderWebhookInboxHandler, StripeWebhookInboxHandler>()
            .BuildServiceProvider();

        // Act and assert
        Assert.False(CreateWebhookEndpoint.TryGetInbox(withoutInbox, out _));
        Assert.False(CreateWebhookEndpoint.TryGetInbox(inboxOnly, out _));
        Assert.True(CreateWebhookEndpoint.TryGetInbox(inboxAndHandler, out var inbox));
        Assert.NotNull(inbox);
    }

    private static async Task CreateSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ProcessedStripeWebhookEventIndex>(table => table
            .Column<string>("EventId", column => column.WithLength(66))
            .Column<DateTime>("ProcessedUtc"));
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private sealed class CountingPaymentEvent : PaymentEventBase
    {
        public int SucceededCount { get; private set; }

        public override Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context, CancellationToken cancellationToken = default)
        {
            SucceededCount++;

            return Task.CompletedTask;
        }
    }
}
