using CrestApps.OrchardCore.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core;

/// <summary>
/// A data migration that skips records keeps a summary an administrator sees in the admin until they dismiss it, not
/// only a line in the log.
/// </summary>
public sealed class MigrationSkipLogTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ASkippingMigration_RecordsItsSummary_UntilItIsDismissed()
    {
        // Arrange
        var document = new MigrationSkipLogDocument();
        var log = new MigrationSkipLog(DocumentManager(document), Clock());
        var services = new ServiceCollection().AddSingleton<IMigrationSkipLog>(log).BuildServiceProvider();

        var report = new MigrationSkipReport("AIProfileDocumentMigrations", NullLogger.Instance);
        report.Skip("profile-1", "validation failed");
        report.Skip("profile-2", "validation failed");

        // Act
        await report.LogSummaryAsync(services);
        var entries = await log.ListAsync(TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(entries);
        Assert.Equal("AIProfileDocumentMigrations", entry.MigrationName);
        Assert.Equal(2, entry.SkippedCount);
        Assert.Equal(["profile-1", "profile-2"], entry.SkippedIds);
        Assert.Equal(_now, entry.RecordedUtc);

        await log.DismissAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await log.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AMigrationThatSkippedNothing_RecordsNothing_AndAMissingLogIsFine()
    {
        var document = new MigrationSkipLogDocument();
        var log = new MigrationSkipLog(DocumentManager(document), Clock());
        var report = new MigrationSkipReport("Clean", NullLogger.Instance);

        await log.RecordAsync(report, TestContext.Current.CancellationToken);
        await report.LogSummaryAsync(new ServiceCollection().BuildServiceProvider());

        Assert.Empty(document.Entries);
    }

    [Fact]
    public async Task TheLog_KeepsOnlyTheNewestSummaries()
    {
        var document = new MigrationSkipLogDocument();
        var log = new MigrationSkipLog(DocumentManager(document), Clock());

        for (var i = 0; i < MigrationSkipLog.MaxEntries + 5; i++)
        {
            var report = new MigrationSkipReport($"Migration{i}", NullLogger.Instance);
            report.Skip("r", "reason");

            await log.RecordAsync(report, TestContext.Current.CancellationToken);
        }

        Assert.Equal(MigrationSkipLog.MaxEntries, document.Entries.Count);
        Assert.Equal("Migration5", document.Entries[0].MigrationName);
    }

    private static IDocumentManager<MigrationSkipLogDocument> DocumentManager(MigrationSkipLogDocument document)
    {
        var manager = new Mock<IDocumentManager<MigrationSkipLogDocument>>();
        manager.Setup(value => value.GetOrCreateMutableAsync(It.IsAny<Func<Task<MigrationSkipLogDocument>>>())).ReturnsAsync(document);
        manager.Setup(value => value.GetOrCreateImmutableAsync(It.IsAny<Func<Task<MigrationSkipLogDocument>>>())).ReturnsAsync(document);

        return manager.Object;
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }
}
