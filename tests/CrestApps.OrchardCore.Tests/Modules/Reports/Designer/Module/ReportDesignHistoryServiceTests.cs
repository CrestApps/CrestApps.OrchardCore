using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Indexes;
using CrestApps.OrchardCore.Reports.Designer.Migrations;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// Drafts, publishing, versions, and the revision check, over a real SQLite store whose tables the module's migration
/// creates.
/// </summary>
public sealed class ReportDesignHistoryServiceTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ClaimsPrincipal _ada = User("ada", "Ada");
    private static readonly ClaimsPrincipal _bob = User("bob", "Bob");

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"report-history-{Guid.NewGuid():N}.db");
    private readonly List<ReportDesignChange> _changes = [];
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ReportDesignDraftIndexProvider(), new ReportDesignVersionIndexProvider()]);

        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migrations = new ReportDesignHistoryMigrations
        {
            SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction),
        };

        Assert.Equal(1, await migrations.CreateAsync());
        Assert.Equal(2, await migrations.UpdateFrom1Async());

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        // Deleting retries while another test's connection still holds the file under a parallel run.
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SaveDraft_KeepsChangesOutOfThePublishedReport()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var design = await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);

        // Act
        var result = await history.SaveDraftAsync(design, Edited("Sales by region"), 0, force: false, _ada);
        var workingCopy = await history.GetWorkingCopyAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(ReportHistoryStatus.Saved, result.Status);
        Assert.Equal(1, result.Revision);
        Assert.Equal("Sales", (await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);
        Assert.True(workingCopy.HasDraft);
        Assert.Equal("Sales by region", workingCopy.Design.DisplayText);
        Assert.Equal("owner", workingCopy.Design.OwnerId);
        Assert.Equal("Ada", workingCopy.ModifiedByName);
        Assert.Equal(1, workingCopy.Revision);
        var change = Assert.Single(_changes);
        Assert.Equal(ReportDesignChangeKind.DraftSaved, change.Kind);
        Assert.Equal(1, change.Revision);
        Assert.Equal("Ada", change.UserName);
    }

    [Fact]
    public async Task SaveDraft_BasedOnAnOlderRevision_IsRefused_UnlessForced()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var design = await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);
        await history.SaveDraftAsync(design, Edited("Ada's title"), 0, force: false, _ada);

        // Act
        var refused = await history.SaveDraftAsync(design, Edited("Bob's title"), 0, force: false, _bob);
        var forced = await history.SaveDraftAsync(design, Edited("Bob's title"), 0, force: true, _bob);

        // Assert
        Assert.Equal(ReportHistoryStatus.Conflict, refused.Status);
        Assert.Equal(1, refused.Revision);
        Assert.Equal("Ada", refused.ModifiedByName);
        Assert.Equal(ReportHistoryStatus.Saved, forced.Status);
        Assert.Equal(2, forced.Revision);
        Assert.Equal("Bob's title", (await history.GetWorkingCopyAsync(design)).Design.DisplayText);
    }

    [Fact]
    public async Task Publish_KeepsTheEarlierStateAndANewVersion_OnlyWhenSomethingChanged()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var design = await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);
        var draft = await history.SaveDraftAsync(design, Edited("Sales by region"), 0, force: false, _ada);

        // Act
        var published = await history.PublishAsync(Edited("Sales by region"), design, draft.Revision, force: false, _ada, canSharePublicly: false);
        var unchanged = await history.PublishAsync(Edited("Sales by region"), await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), published.Revision, force: false, _ada, canSharePublicly: false);

        // Assert
        Assert.Equal(ReportHistoryStatus.Saved, published.Status);
        Assert.True(published.Save.Saved);
        Assert.Equal(2, published.VersionNumber);
        Assert.Null(unchanged.VersionNumber);
        Assert.Equal("Sales by region", (await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);

        var versions = await history.ListVersionsAsync("r1");
        Assert.Equal([2, 1], versions.Select(version => version.Number));
        Assert.Equal(["Sales by region", "Sales"], versions.Select(version => version.DisplayText));
        Assert.Equal("Ada", versions[0].CreatedByName);
        Assert.False((await history.GetWorkingCopyAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken))).HasDraft);
        Assert.Equal(ReportDesignChangeKind.Published, _changes[1].Kind);
        Assert.Equal(2, _changes[1].VersionNumber);
    }

    [Fact]
    public async Task Publish_BasedOnAnOlderRevision_IsRefused_AndChangesNothing()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var design = await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);
        await history.SaveDraftAsync(design, Edited("Ada's title"), 0, force: false, _ada);

        // Act
        var result = await history.PublishAsync(Edited("Bob's title"), design, 0, force: false, _bob, canSharePublicly: false);

        // Assert
        Assert.Equal(ReportHistoryStatus.Conflict, result.Status);
        Assert.Equal("Sales", (await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);
        Assert.Empty(await history.ListVersionsAsync("r1"));
    }

    [Fact]
    public async Task Publish_ANewReport_CreatesItWithItsFirstVersion()
    {
        // Arrange
        var designs = Catalog<ReportDesign>();
        await using var session = _store.CreateSession();
        var history = History(session, designs);

        // Act
        var result = await history.PublishAsync(Edited("New report"), null, 0, force: false, _ada, canSharePublicly: false);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportHistoryStatus.Saved, result.Status);
        Assert.Equal(1, result.VersionNumber);
        Assert.Equal(1, result.Revision);
        Assert.Equal("ada", (await designs.FindByIdAsync(result.Save.Id, TestContext.Current.CancellationToken)).OwnerId);
        Assert.Equal(1, Assert.Single(await history.ListVersionsAsync(result.Save.Id)).Number);
    }

    [Fact]
    public async Task Restore_CopiesTheVersionIntoTheDraft_AndPublishingRecordsWhereItCameFrom()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var first = await history.PublishAsync(Edited("Second title"), await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), 0, force: false, _ada, canSharePublicly: false);

        // Act
        var restored = await history.RestoreAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), 1, first.Revision, force: false, _bob);
        var workingCopy = await history.GetWorkingCopyAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken));
        var published = await history.PublishAsync(workingCopy.Design, await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), restored.Revision, force: false, _bob, canSharePublicly: false);

        // Assert
        Assert.Equal(ReportHistoryStatus.Saved, restored.Status);
        Assert.Equal("Sales", workingCopy.Design.DisplayText);
        Assert.Equal("Second title", (await history.FindVersionAsync("r1", 2)).Design.DisplayText);
        Assert.Equal(3, published.VersionNumber);
        Assert.Equal(1, (await history.ListVersionsAsync("r1"))[0].RestoredFrom);
        Assert.Equal("Sales", (await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);
    }

    [Fact]
    public async Task Restore_AMissingVersion_IsNotFound()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);

        // Act
        var result = await history.RestoreAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), 9, 0, force: false, _ada);

        // Assert
        Assert.Equal(ReportHistoryStatus.NotFound, result.Status);
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task Discard_ReturnsToThePublishedReport_AndMovesTheRevisionOn()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var design = await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken);
        var draft = await history.SaveDraftAsync(design, Edited("Draft title"), 0, force: false, _ada);

        // Act
        var discarded = await history.DiscardDraftAsync(design, draft.Revision, force: false, _ada);
        var stale = await history.SaveDraftAsync(design, Edited("Late change"), draft.Revision, force: false, _bob);
        var workingCopy = await history.GetWorkingCopyAsync(design);

        // Assert
        Assert.Equal(2, discarded.Revision);
        Assert.Equal(ReportHistoryStatus.Conflict, stale.Status);
        Assert.False(workingCopy.HasDraft);
        Assert.Equal("Sales", workingCopy.Design.DisplayText);
    }

    [Fact]
    public async Task Versions_BeyondTheLimit_AreDeleted_OldestFirst()
    {
        // Arrange
        var designs = Catalog(Published("Title 0"));
        await using var session = _store.CreateSession();
        var history = History(session, designs, maxVersions: 2);
        var revision = 0L;

        // Act
        foreach (var title in new[] { "Title 1", "Title 2", "Title 3" })
        {
            revision = (await history.PublishAsync(Edited(title), await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), revision, force: false, _ada, canSharePublicly: false)).Revision;
        }

        // Assert
        var versions = await history.ListVersionsAsync("r1");
        Assert.Equal([4, 3], versions.Select(version => version.Number));
    }

    [Fact]
    public async Task Delete_RemovesTheDraftAndVersions_AndTellsTheEditors()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var published = await history.PublishAsync(Edited("Second title"), await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), 0, force: false, _ada, canSharePublicly: false);
        await history.SaveDraftAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), Edited("Draft"), published.Revision, force: false, _ada);

        // Act
        await history.DeleteAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), _bob);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken));
        Assert.Empty(await history.ListVersionsAsync("r1"));
        Assert.Null(await new ReportDesignHistoryStore(session).FindDraftAsync("r1"));
        Assert.Equal(ReportDesignChangeKind.Deleted, _changes[^1].Kind);
        Assert.Equal("Bob", _changes[^1].UserName);
    }

    [Fact]
    public async Task Changes_WhenTheLockIsNotAvailable_AreRefusedAsBusy()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs, locked: false);

        // Act
        var draft = await history.SaveDraftAsync(await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), Edited("Title"), 0, force: false, _ada);
        var published = await history.PublishAsync(Edited("Title"), await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken), 0, force: true, _ada, canSharePublicly: false);

        // Assert
        Assert.Equal(ReportHistoryStatus.Busy, draft.Status);
        Assert.Equal(ReportHistoryStatus.Busy, published.Status);
        Assert.Equal("Sales", (await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);
    }

    [Fact]
    public async Task NewReport_IsSavedAsADraft_ThenPublishedWithTheSameIdentifier()
    {
        // Arrange
        var designs = Catalog<ReportDesign>();
        await using var session = _store.CreateSession();
        var history = History(session, designs);

        // Act
        var created = await history.CreateDraftAsync(Edited("Work in progress"), _ada);
        var unpublished = await history.FindUnpublishedAsync(created.DesignId);
        var saved = await history.SaveDraftAsync(unpublished.Design, Edited("Almost done"), created.Revision, force: false, _ada);
        var listed = (await history.ListUnpublishedAsync()).Select(draft => draft.Design.DisplayText).ToArray();
        var published = await history.PublishAsync(Edited("Done", created.DesignId), null, saved.Revision, force: false, _ada, canSharePublicly: false);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, created.Revision);
        Assert.Equal("ada", unpublished.Design.OwnerId);
        Assert.Equal("Almost done", Assert.Single(listed));
        Assert.Equal(ReportHistoryStatus.Saved, published.Status);
        Assert.Equal(1, published.VersionNumber);
        Assert.Equal(created.DesignId, published.Save.Id);
        Assert.Equal("Done", (await designs.FindByIdAsync(created.DesignId, TestContext.Current.CancellationToken)).DisplayText);
        Assert.Null(await history.FindUnpublishedAsync(created.DesignId));
        Assert.Empty(await history.ListUnpublishedAsync());
    }

    [Fact]
    public async Task NewReport_PublishedFromAStaleRevision_IsRefused()
    {
        // Arrange
        var designs = Catalog<ReportDesign>();
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var created = await history.CreateDraftAsync(Edited("Draft"), _ada);
        await history.SaveDraftAsync((await history.FindUnpublishedAsync(created.DesignId)).Design, Edited("Bob's change"), created.Revision, force: false, _bob);

        // Act
        var result = await history.PublishAsync(Edited("Ada's title", created.DesignId), null, created.Revision, force: false, _ada, canSharePublicly: false);

        // Assert
        Assert.Equal(ReportHistoryStatus.Conflict, result.Status);
        Assert.Equal("Bob", result.ModifiedByName);
        Assert.Null(await designs.FindByIdAsync(created.DesignId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Publish_WithTheIdentifierOfAPublishedReportButNoExisting_IsNotFound()
    {
        // Arrange
        var designs = Catalog(Published("Sales"));
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var incoming = Edited("Hijack");
        incoming.ItemId = "r1";

        // Act
        var result = await history.PublishAsync(incoming, null, 0, force: true, _bob, canSharePublicly: false);

        // Assert
        Assert.Equal(ReportHistoryStatus.NotFound, result.Status);
        Assert.Equal("Sales", (await designs.FindByIdAsync("r1", TestContext.Current.CancellationToken)).DisplayText);
    }

    [Fact]
    public async Task DeleteUnpublished_RemovesTheDraft()
    {
        // Arrange
        var designs = Catalog<ReportDesign>();
        await using var session = _store.CreateSession();
        var history = History(session, designs);
        var created = await history.CreateDraftAsync(Edited("Scratch"), _ada);

        // Act
        var stale = await history.DeleteUnpublishedAsync(created.DesignId, 0, force: false, _ada);
        var deleted = await history.DeleteUnpublishedAsync(created.DesignId, created.Revision, force: false, _ada);

        // Assert
        Assert.Equal(ReportHistoryStatus.Conflict, stale.Status);
        Assert.Equal(ReportHistoryStatus.Saved, deleted.Status);
        Assert.Null(await history.FindUnpublishedAsync(created.DesignId));
        Assert.Equal(ReportDesignChangeKind.Deleted, _changes[^1].Kind);
    }

    [Fact]
    public void HasSameContent_IgnoresIdentityAndDates()
    {
        // Arrange
        var left = Published("Sales");
        var right = Published("Sales");
        right.ItemId = "other";
        right.ModifiedUtc = _now.AddDays(1);

        // Act & Assert
        Assert.True(ReportDesignHistoryService.HasSameContent(left, right));
        right.AllowExport = !right.AllowExport;
        Assert.False(ReportDesignHistoryService.HasSameContent(left, right));
    }

    private ReportDesignHistoryService History(ISession session, ICatalog<ReportDesign> designs, int maxVersions = 50, bool locked = true)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => (locked ? Mock.Of<ILocker>() : null, locked));

        var notifier = new Mock<IReportDesignNotifier>();
        notifier
            .Setup(value => value.ReportDesignChangedAsync(It.IsAny<ReportDesignChange>()))
            .Callback((ReportDesignChange change) => _changes.Add(change))
            .Returns(Task.CompletedTask);

        var designService = new ReportDesignService(
            designs,
            Catalog<ReportView>(),
            new ReportShareLinkService(Catalog<ReportShareLink>(), clock.Object),
            Planner(SalesData()),
            DocumentBuilder(),
            clock.Object,
            new PassThroughStringLocalizer<ReportDesignService>());

        return new ReportDesignHistoryService(
            designService,
            new ReportDesignHistoryStore(session),
            distributedLock.Object,
            notifier.Object,
            Options.Create(new ReportDesignVersionOptions { MaxVersions = maxVersions }),
            clock.Object,
            NullLogger<ReportDesignHistoryService>.Instance);
    }

    private static ReportDesign Published(string title)
    {
        return new ReportDesign
        {
            ItemId = "r1",
            DisplayText = title,
            OwnerId = "owner",
            Author = "Owner",
            CreatedUtc = _now.AddDays(-10),
            Query = Query(),
        };
    }

    // The report as the builder sends it; a report that exists only as a draft is sent with its identifier.
    private static ReportDesign Edited(string title, string itemId = null)
    {
        return new ReportDesign
        {
            ItemId = itemId,
            DisplayText = title,
            Query = Query(),
        };
    }

    private static ReportQueryDefinition Query()
    {
        var query = CustomersWithOrders();

        query.Columns = [Column("name", "c.Name")];

        return query;
    }
}
