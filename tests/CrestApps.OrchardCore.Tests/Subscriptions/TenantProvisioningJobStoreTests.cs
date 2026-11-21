using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C16: a legacy tenant entitlement finds its tenant's job by name. The store took the first job with the name,
/// unordered, so a failed "name in use" job of another buyer could answer instead of the one that created the tenant.
/// </summary>
public sealed class TenantProvisioningJobStoreTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-provisioning-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new TenantProvisioningJobIndexProvider()], SubscriptionConstants.TenantProvisioningCollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(SubscriptionConstants.TenantProvisioningCollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new TenantProvisioningJobMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetByTenantNameAsync_WithAFailedJobOfTheSameName_ReturnsTheJobThatCreatedTheTenant()
    {
        // Arrange: the job that created Shop1, then another buyer's job for the same name that failed.
        await using (var session = _store.CreateSession())
        {
            var store = CreateStore(session);

            await store.CreateAsync(new TenantProvisioningJob { ItemId = "job-created", TenantName = "Shop1", SubscriptionId = "sub-owner", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now }, TestContext.Current.CancellationToken);
            await store.CreateAsync(new TenantProvisioningJob { ItemId = "job-failed", TenantName = "Shop1", SubscriptionId = "sub-other", Status = TenantProvisioningStatus.Failed, CreatedUtc = _now.AddDays(1) }, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using var reading = _store.CreateSession();
        var job = await CreateStore(reading).GetByTenantNameAsync("Shop1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("job-created", job?.ItemId);
    }

    private static TenantProvisioningJobStore CreateStore(ISession session)
        => new(session, Mock.Of<IClock>(clock => clock.UtcNow == _now));
}
