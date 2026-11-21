using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Core.Indexes;
using CrestApps.OrchardCore.Wizard.Core.Migrations;
using CrestApps.OrchardCore.Wizard.Core.Services;
using Xunit;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Wizard;

/// <summary>
/// SEC-A18: abandoned wizard sessions were never removed. Pending sessions not touched within the retention window are
/// purged; completed sessions are kept.
/// </summary>
public sealed class WizardSessionRetentionPolicyTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"wizard-retention-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new WizardSessionIndexProvider()]);

        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migrations = new WizardMigrations
        {
            SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction),
        };

        var version = await migrations.CreateAsync();

        if (version == 1)
        {
            await migrations.UpdateFrom1Async();
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PurgeBatchAsync_RemovesOnlyAbandonedPendingSessions()
    {
        var old = _now.AddDays(-(WizardSessionRetentionPolicy.RetentionDays + 1));

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new WizardSession { SessionId = "abandoned", WizardType = "w", Status = WizardSessionStatus.Pending, CreatedUtc = old, ModifiedUtc = old }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new WizardSession { SessionId = "recent", WizardType = "w", Status = WizardSessionStatus.Pending, CreatedUtc = _now, ModifiedUtc = _now }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new WizardSession { SessionId = "done", WizardType = "w", Status = WizardSessionStatus.Completed, CreatedUtc = old, ModifiedUtc = old, CompletedUtc = old }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        int purged;

        await using (var session = _store.CreateSession())
        {
            var policy = new WizardSessionRetentionPolicy(session);

            Assert.True(policy.TryGetCutoff(_now, out var cutoff));
            purged = await policy.PurgeBatchAsync(cutoff, 100, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var left = await session.Query<WizardSession, WizardSessionIndex>().ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, purged);
            Assert.Equal(["done", "recent"], left.Select(s => s.SessionId).OrderBy(id => id, StringComparer.Ordinal));
        }
    }
}
