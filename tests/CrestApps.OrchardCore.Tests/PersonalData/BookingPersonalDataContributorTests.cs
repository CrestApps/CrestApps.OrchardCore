using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Migrations;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// A person's bookings are found by their account and, for guest bookings, by their email through the indexed columns
/// the migration adds; erasing keeps the booking's dates and amounts but removes the booker and the travelers.
/// </summary>
public sealed class BookingPersonalDataContributorTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-pd-bookings-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new BookingSlotIndexProvider(), new BookingIndexProvider(), new BookingBalanceIndexProvider()], BookingsConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(BookingsConstants.CollectionName, cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await new BookingMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ABookersAccountAndGuestBookings_AreExported_AndAnonymizedKeepingTheDates()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(Booking("b-account", userId: "user-1", email: "ann@example.com", orderId: "o-1"), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Booking("b-guest", userId: null, email: "Ann@Example.com", orderId: "o-2"), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Booking("b-other", userId: "user-2", email: "bob@example.com", orderId: "o-3"), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new BookingBalance { ItemId = "bal-1", OrderId = "o-1", OrderNumber = "N-1", CustomerName = "Ann Lee", CustomerEmail = "ann@example.com", Amount = 50m, Currency = "USD" }, false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Act
        PersonalDataExport export;

        await using (var session = _store.CreateSession())
        {
            var contributor = CreateContributor(session);
            var subject = new PersonalDataSubject(userId: "user-1");

            // The account's booking tells the contributor the person's email, which finds the guest booking.
            await contributor.IdentifyAsync(subject, cancellationToken);
            export = new PersonalDataExport();
            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), cancellationToken);

            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, new PersonalDataAnonymizeResult()), cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Assert
        Assert.Equal(["b-account", "b-guest"], export.Records.Where(record => record.Kind == BookingPersonalDataContributor.BookingKind).Select(record => record.Id).Order());
        Assert.Single(export.Records, record => record.Kind == BookingPersonalDataContributor.BalanceKind && record.Id == "bal-1");

        await using (var session = _store.CreateSession())
        {
            var bookings = (await session.Query<Booking, BookingIndex>(collection: BookingsConstants.CollectionName).ListAsync(cancellationToken)).ToDictionary(booking => booking.ItemId);

            Assert.Null(bookings["b-account"].CustomerName);
            Assert.Null(bookings["b-account"].CustomerEmail);
            Assert.Empty(bookings["b-account"].Travelers);
            Assert.Equal(new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc), bookings["b-account"].StartsUtc);
            Assert.Null(bookings["b-guest"].CustomerEmail);
            Assert.Equal("bob@example.com", bookings["b-other"].CustomerEmail);

            var balance = await session.Query<BookingBalance, BookingBalanceIndex>(collection: BookingsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);
            Assert.Null(balance.CustomerEmail);
            Assert.Equal(50m, balance.Amount);
        }
    }

    private static BookingPersonalDataContributor CreateContributor(ISession session)
        => new(new BookingStore(session, new ServiceCollection().BuildServiceProvider()), new BookingBalanceStore(session));

    private static Booking Booking(string id, string userId, string email, string orderId)
        => new()
        {
            ItemId = id,
            OrderId = orderId,
            ContentItemId = "product-1",
            Title = "River tour",
            UserId = userId,
            CustomerName = "Ann Lee",
            CustomerEmail = email,
            StartsUtc = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc),
            CreatedUtc = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            Status = BookingStatus.Confirmed,
            Travelers = [new JsonObject { ["Name"] = "Ann Lee", ["Passport"] = "X123" }],
        };
}
