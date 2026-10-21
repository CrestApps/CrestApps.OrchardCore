using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Runs the queries that find a person's checkouts through a real YesSql store, because what they pin is in the SQL:
/// a guest checkout is found only by the email its buyer gave, matched without case sensitivity, and a missing owner or
/// email must never turn into a match on every session without one.
/// </summary>
public sealed class CheckoutSessionCustomerIndexSqliteTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetByCustomerAsync_FindsSessionsByOwnerAndByEmailWithoutCaseSensitivity()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-checkout-customer-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await SeedAsync(store,
                Session("s1", ownerId: "user-1", minutes: 1),
                Session("s2", email: "Pat@Example.test", minutes: 2),
                Session("s3", minutes: 3),
                Session("s4", ownerId: "user-2", email: "pat@example.test", minutes: 4),
                Session("s5", email: "sam@example.test", minutes: 5));

            await using var session = store.CreateSession();
            var sessionStore = CreateSessionStore(session);

            // Act
            var byOwner = await sessionStore.GetByCustomerAsync("user-1", null, 0, 10, TestContext.Current.CancellationToken);
            var byEmail = await sessionStore.GetByCustomerAsync(null, " PAT@example.TEST ", 0, 10, TestContext.Current.CancellationToken);
            var byBoth = await sessionStore.GetByCustomerAsync("user-1", "pat@example.test", 0, 10, TestContext.Current.CancellationToken);
            var paged = await sessionStore.GetByCustomerAsync("user-1", "pat@example.test", 1, 1, TestContext.Current.CancellationToken);
            var byNothing = await sessionStore.GetByCustomerAsync(null, " ", 0, 10, TestContext.Current.CancellationToken);
            var many = await sessionStore.GetManyAsync(["s1", "s3", "missing", "s1"], TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(new[] { "s1" }, byOwner.Select(item => item.SessionId));
            Assert.Equal(new[] { "s2", "s4" }, byEmail.Select(item => item.SessionId));
            Assert.Equal(new[] { "s1", "s2", "s4" }, byBoth.Select(item => item.SessionId));
            Assert.Equal(new[] { "s2" }, paged.Select(item => item.SessionId));
            Assert.Empty(byNothing);
            Assert.Equal(new[] { "s1", "s3" }, many.Select(item => item.SessionId).Order(StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task ListByReferenceAsync_ReturnsEveryAttemptForTheReference_OldestFirst()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-checkout-reference-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await SeedAsync(store,
                Session("s2", referenceId: "o1", minutes: 2),
                Session("s1", referenceId: "o1", minutes: 1),
                Session("s3", referenceId: "o2", minutes: 3),
                Session("s4", referenceId: "o1", minutes: 4));

            await using var session = store.CreateSession();
            var sessionStore = CreateSessionStore(session);

            // Act
            var all = await sessionStore.ListByReferenceAsync(CheckoutReferenceTypes.Order, "o1", 0, 10, TestContext.Current.CancellationToken);
            var paged = await sessionStore.ListByReferenceAsync(CheckoutReferenceTypes.Order, "o1", 1, 1, TestContext.Current.CancellationToken);
            var otherType = await sessionStore.ListByReferenceAsync("Transaction", "o1", 0, 10, TestContext.Current.CancellationToken);
            var newest = await sessionStore.GetByReferenceAsync(CheckoutReferenceTypes.Order, "o1", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(new[] { "s1", "s2", "s4" }, all.Select(item => item.SessionId));
            Assert.Equal(new[] { "s2" }, paged.Select(item => item.SessionId));
            Assert.Empty(otherType);
            Assert.Equal("s4", newest.SessionId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static CheckoutSession Session(string sessionId, string ownerId = null, string email = null, int minutes = 0, string referenceId = null)
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            ReferenceType = referenceId is null ? null : CheckoutReferenceTypes.Order,
            ReferenceId = referenceId,
            OwnerId = ownerId,
            Status = CheckoutSessionStatus.Completed,
            CreatedUtc = _now.AddMinutes(minutes),
            ModifiedUtc = _now.AddMinutes(minutes),
        };

        if (email is not null)
        {
            session.Put(new CheckoutContactInfo
            {
                DisplayName = "Pat Lee",
                Email = email,
            });
        }

        return session;
    }

    private static async Task SeedAsync(IStore store, params CheckoutSession[] sessions)
    {
        await using var session = store.CreateSession();

        foreach (var checkoutSession in sessions)
        {
            await session.SaveAsync(checkoutSession, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static CheckoutSessionStore CreateSessionStore(ISession session)
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        // The lookups under test read the index only; the request-bound collaborators are never touched.
        return new CheckoutSessionStore(
            null,
            null,
            null,
            [],
            NullLogger<CheckoutSessionStore>.Instance,
            clock.Object,
            session);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CheckoutSessionIndexProvider(), new CheckoutSessionCustomerIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await new CheckoutMigrations { SchemaBuilder = new SchemaBuilder(store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
