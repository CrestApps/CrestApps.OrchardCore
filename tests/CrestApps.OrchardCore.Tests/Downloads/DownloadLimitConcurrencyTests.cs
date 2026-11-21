using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Migrations;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Downloads;

/// <summary>
/// SEC-C17n: two downloads of the last one a link allows, each request having read the link before the other counted,
/// don't both go through: the count is read again from the database inside the lock.
/// </summary>
public sealed class DownloadLimitConcurrencyTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"download-limit-{Guid.NewGuid():N}.db");
    private readonly IDistributedLock _locks = new LocalLock(NullLogger<LocalLock>.Instance);
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new DownloadEntitlementIndexProvider()], DownloadsConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(DownloadsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await new DownloadMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new DownloadEntitlement { ItemId = "entitlement-1", OrderId = "order-1", FileName = "guide.pdf", DownloadLimit = 1, DownloadCount = 0 }, false, DownloadsConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RecordDownloadAsync_TwoRequestsThatReadTheLinkBeforeEitherCounted_CountOnlyOne()
    {
        // Arrange: both requests read the link, with one download left.
        await using var first = _store.CreateSession();
        await using var second = _store.CreateSession();

        var firstEntitlement = await LoadAsync(first);
        var secondEntitlement = await LoadAsync(second);

        // Act
        var firstRecorded = await CreateService(first).RecordDownloadAsync(firstEntitlement, TestContext.Current.CancellationToken);
        var secondRecorded = await CreateService(second).RecordDownloadAsync(secondEntitlement, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(firstRecorded);
        Assert.False(secondRecorded);
    }

    private static async Task<DownloadEntitlement> LoadAsync(ISession session)
        => await session.Query<DownloadEntitlement, DownloadEntitlementIndex>(index => index.ItemId == "entitlement-1", collection: DownloadsConstants.CollectionName)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

    private DownloadService CreateService(ISession session)
        => new(
            session,
            Mock.Of<ISiteService>(),
            new EphemeralDataProtectionProvider(),
            (SiteUrlGenerator)RuntimeHelpers.GetUninitializedObject(typeof(SiteUrlGenerator)),
            _locks,
            Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)));
}
