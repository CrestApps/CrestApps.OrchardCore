using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.PointOfSale.Migrations;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// The shifts a staff member opened or closed are found through the user columns the migration adds to the shift index;
/// erasing replaces their name and keeps the cash tallies.
/// </summary>
public sealed class RegisterShiftPersonalDataContributorTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-pd-pos-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new RegisterIndexProvider(), new RegisterShiftIndexProvider()], PosConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(PosConstants.CollectionName, cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await new PosMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ShiftsAStaffMemberOpenedOrClosed_AreExported_AndTheirNameReplaced()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(Shift("s-opened", openedBy: "user-1", closedBy: "user-2"), false, PosConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Shift("s-closed", openedBy: "user-2", closedBy: "user-1"), false, PosConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Shift("s-other", openedBy: "user-2", closedBy: "user-2"), false, PosConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        var subject = new PersonalDataSubject(userId: "user-1");
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();

        // Act
        await using (var session = _store.CreateSession())
        {
            var contributor = new RegisterShiftPersonalDataContributor(new RegisterShiftStore(session));

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), cancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, "Former staff", result), cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Assert
        Assert.Equal(["s-closed", "s-opened"], export.Records.Select(record => record.Id).Order());
        Assert.Equal(2, result.Records[RegisterShiftPersonalDataContributor.RegisterShiftKind]);

        await using (var session = _store.CreateSession())
        {
            var shifts = (await session.Query<RegisterShift, RegisterShiftIndex>(collection: PosConstants.CollectionName).ListAsync(cancellationToken)).ToDictionary(shift => shift.ItemId);

            Assert.Equal("Former staff", shifts["s-opened"].OpenedByName);
            Assert.Equal("Bob", shifts["s-opened"].ClosedByName);
            Assert.Equal("Former staff", shifts["s-closed"].ClosedByName);
            Assert.Equal(100m, shifts["s-closed"].CountedCash);
            Assert.Equal("Bob", shifts["s-other"].OpenedByName);
        }
    }

    private static RegisterShift Shift(string id, string openedBy, string closedBy)
        => new()
        {
            ItemId = id,
            RegisterId = "register-1",
            RegisterName = "Front",
            OpenedByUserId = openedBy,
            OpenedByName = openedBy == "user-1" ? "Ann" : "Bob",
            ClosedByUserId = closedBy,
            ClosedByName = closedBy == "user-1" ? "Ann" : "Bob",
            OpenedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
            ClosedUtc = new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc),
            CountedCash = 100m,
            Status = RegisterShiftStatus.Closed,
        };
}
