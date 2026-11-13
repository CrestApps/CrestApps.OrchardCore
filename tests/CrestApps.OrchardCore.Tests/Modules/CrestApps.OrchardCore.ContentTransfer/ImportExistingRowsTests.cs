using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

/// <summary>
/// SEC-A7: the rows of an import that carry an item id go through
/// <see cref="ImportFilesBackgroundTask.ImportExistingRowsAsync"/>, the path the batch import runs. A row that names
/// an item of another content type is refused, and refusing it must leave that item exactly as it was.
/// </summary>
public sealed class ImportExistingRowsTests
{
    [Fact]
    public async Task ARowNamingAPublishedItemOfAnotherType_IsRefused_AndTheItemStaysPublished()
    {
        var page = new ContentItem { ContentItemId = "4xpage0000000000000000000a", ContentType = "Page", Published = true, Latest = true, Owner = "owner" };
        var site = new FakeContentSite(page);
        var progress = new ImportFileProcessStatsPart();

        await ImportAsync(site, progress, page.ContentItemId);

        Assert.True(page.Published, "Refusing the row must not unpublish the item it named.");
        Assert.Empty(site.Saved);
        Assert.Contains(0, progress.Errors);
        Assert.Equal(0, progress.ImportedCount);
    }

    [Fact]
    public async Task ARowNamingAnotherTypesItemInAnotherCase_IsRefused_AndCreatesNothing()
    {
        // On a case-insensitive collation (the SQL Server and MySQL defaults) the database returns the item for an id
        // in the other case. The row must still be matched to it, and refused, instead of creating an item that takes
        // the id from the file.
        var page = new ContentItem { ContentItemId = "4xpage0000000000000000000a", ContentType = "Page", Published = true, Latest = true, Owner = "owner" };
        var site = new FakeContentSite(page);
        var progress = new ImportFileProcessStatsPart();

        await ImportAsync(site, progress, page.ContentItemId.ToUpperInvariant());

        Assert.True(page.Published);
        Assert.Empty(site.Created);
        Assert.Empty(site.Saved);
        Assert.Contains(0, progress.Errors);
    }

    [Fact]
    public async Task ARowNamingAnItemOfTheImportsType_UpdatesIt_AndKeepsItsOwner()
    {
        var article = new ContentItem { ContentItemId = "4xarticle00000000000000000", ContentType = "Article", Published = true, Latest = true, Owner = "owner", Author = "owner" };
        var site = new FakeContentSite(article);
        var progress = new ImportFileProcessStatsPart();

        await ImportAsync(site, progress, article.ContentItemId);

        Assert.Equal([article.ContentItemId], site.Saved);
        Assert.Equal(("owner", "owner"), (article.Owner, article.Author));
        Assert.Equal(1, progress.ImportedCount);
    }

    [Fact]
    public async Task ARowNamingAnItemWhoseLatestVersionIsADraft_UpdatesThatDraft_AndCreatesNothing()
    {
        // An import without "publish" leaves its items as drafts. Importing the same file again must update them, not
        // create a second item with the same id and the uploader as its owner.
        var article = new ContentItem { ContentItemId = "4xarticle00000000000000000", ContentType = "Article", Published = false, Latest = true, Owner = "owner", Author = "owner" };
        var site = new FakeContentSite(article);
        var progress = new ImportFileProcessStatsPart();

        await ImportAsync(site, progress, article.ContentItemId);

        Assert.Empty(site.Created);
        Assert.Equal([article.ContentItemId], site.Saved);
        Assert.Equal(("owner", "owner"), (article.Owner, article.Author));
        Assert.Equal(1, progress.ImportedCount);
    }

    private static Task ImportAsync(FakeContentSite site, ImportFileProcessStatsPart progress, string rowId)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(ContentItem.ContentItemId));
        var row = dataTable.NewRow();
        row[0] = rowId;
        dataTable.Rows.Add(row);

        var existingRows = new Dictionary<string, KeyValuePair<int, DataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            [rowId] = new(0, row),
        };

        var importManager = new Mock<IContentImportManager>();
        importManager.Setup(x => x.ImportAsync(It.IsAny<ContentImportContext>())).Returns(Task.CompletedTask);

        return ImportFilesBackgroundTask.ImportExistingRowsAsync(
            new ContentTransferEntry { ContentType = "Article", Owner = "uploader", Author = "uploader" },
            dataTable,
            existingRows,
            new ContentTypeDefinition("Article", "Article"),
            site.ContentManager,
            importManager.Object,
            progress,
            new ImportRunMonitor(),
            new PassThroughStringLocalizer<ImportExistingRowsTests>(),
            new ImportRunTelemetry(NullLogger.Instance, "entry", firstRowOfRun: 0));
    }

    /// <summary>
    /// A content manager that loads items the way Orchard Core's does: ids are matched ignoring case, as on a
    /// case-insensitive collation, and asking for <see cref="VersionOptions.DraftRequired"/> turns the published
    /// version of a type that is not versionable into a draft and leaves out an item whose latest version is a draft.
    /// </summary>
    private sealed class FakeContentSite
    {
        private readonly List<ContentItem> _items;

        public FakeContentSite(params ContentItem[] items)
        {
            _items = [.. items];

            var manager = new Mock<IContentManager>();

            manager.Setup(x => x.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((IEnumerable<string> ids, VersionOptions options) => Get(ids, options ?? VersionOptions.Published));

            manager.Setup(x => x.NewAsync(It.IsAny<string>()))
                .ReturnsAsync((string contentType) => new ContentItem { ContentItemId = "4xnew0000000000000000000000", ContentType = contentType });
            manager.Setup(x => x.ValidateAsync(It.IsAny<ContentItem>())).ReturnsAsync(new ContentValidateResult());
            manager.Setup(x => x.CreateAsync(It.IsAny<ContentItem>(), It.IsAny<VersionOptions>()))
                .Callback((ContentItem item, VersionOptions _) => Created.Add(item.ContentItemId))
                .ReturnsAsync(true);
            manager.Setup(x => x.UpdateAsync(It.IsAny<ContentItem>()))
                .Callback((ContentItem item) => Saved.Add(item.ContentItemId))
                .Returns(Task.CompletedTask);
            manager.Setup(x => x.PublishAsync(It.IsAny<ContentItem>())).ReturnsAsync(true);
            manager.Setup(x => x.SaveDraftAsync(It.IsAny<ContentItem>())).Returns(Task.CompletedTask);

            ContentManager = manager.Object;
        }

        public IContentManager ContentManager { get; }

        public List<string> Created { get; } = [];

        public List<string> Saved { get; } = [];

        // DefaultContentManager.GetAsync(ids, options) of the pinned Orchard Core 4.0 preview: Latest and DraftRequired read
        // the latest versions, Published the published ones. DraftRequired then returns only the items that were
        // published, each made a draft (a type that is not versionable is unpublished in place); an item whose latest
        // version is already a draft is left out of the result.
        private List<ContentItem> Get(IEnumerable<string> ids, VersionOptions options)
        {
            var named = _items.Where(item => ids.Contains(item.ContentItemId, StringComparer.OrdinalIgnoreCase));

            if (options.IsPublished && !options.IsLatest && !options.IsDraft && !options.IsDraftRequired)
            {
                return [.. named.Where(item => item.Published)];
            }

            var latest = named.Where(item => item.Latest).ToList();

            if (!options.IsDraftRequired)
            {
                return options.IsDraft ? [.. latest.Where(item => !item.Published)] : latest;
            }

            var drafts = new List<ContentItem>();

            foreach (var item in latest.Where(item => item.Published))
            {
                item.Published = false;
                drafts.Add(item);
            }

            return drafts;
        }
    }
}
