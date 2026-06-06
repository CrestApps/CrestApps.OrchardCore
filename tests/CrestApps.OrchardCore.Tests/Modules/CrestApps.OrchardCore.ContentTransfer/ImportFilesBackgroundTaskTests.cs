using System.Data;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using OrchardCore.ContentManagement;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ImportFilesBackgroundTaskTests
{
    [Fact]
    public void GetImportContentItemId_ShouldReturnContentItemId()
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(ContentItem.ContentItemId));
        dataTable.Columns.Add(nameof(ContentItem.ContentItemVersionId));
        var row = dataTable.NewRow();
        row[nameof(ContentItem.ContentItemId)] = "4eb8n2j6ffk0q6dg9jevfavb5y";
        row[nameof(ContentItem.ContentItemVersionId)] = "version-id-from-export";
        dataTable.Rows.Add(row);

        var contentItemId = ImportFilesBackgroundTask.GetImportContentItemId(
            row,
            dataTable.Columns.IndexOf(nameof(ContentItem.ContentItemId)));

        Assert.Equal("4eb8n2j6ffk0q6dg9jevfavb5y", contentItemId);
    }

    [Fact]
    public void GetImportContentItemId_ShouldIgnoreContentItemVersionId()
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(ContentItem.ContentItemVersionId));
        var row = dataTable.NewRow();
        row[nameof(ContentItem.ContentItemVersionId)] = "version-id-from-export";
        dataTable.Rows.Add(row);

        var contentItemId = ImportFilesBackgroundTask.GetImportContentItemId(
            row,
            indexOfKeyColumn: -1);

        Assert.Null(contentItemId);
    }
}
