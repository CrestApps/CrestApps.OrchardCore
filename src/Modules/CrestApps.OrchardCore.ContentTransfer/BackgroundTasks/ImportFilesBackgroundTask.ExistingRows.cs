using System.Data;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

public sealed partial class ImportFilesBackgroundTask
{
    /// <summary>
    /// Imports the rows of a batch that carry the id of an item. A row whose id names an existing item of the content
    /// type the row is imported as updates it; a row whose id names nothing creates a new item; a row whose id names an
    /// item of another type is refused and leaves that item exactly as it was.
    /// </summary>
    /// <remarks>
    /// The items are first read with <see cref="VersionOptions.Latest"/>, which changes nothing. An item of the import's
    /// type whose latest version is a draft is edited as it is. Only the published items of the import's type are then
    /// asked for with <see cref="VersionOptions.DraftRequired"/>, which unpublishes an item whose type is not versionable
    /// and adds a draft to one whose type is (and returns nothing for an item that is already a draft): asking for every
    /// named item that way changed the items of the rows it then refused. Ids are matched without regard to case, as a
    /// case-insensitive database collation matches them, so an id in another case can't pass for a new item and take
    /// the existing one's id.
    /// </remarks>
    /// <param name="rowDefinitionAsync">
    /// Returns the content type a row is imported as, when an import may create items of several related types, or
    /// <see langword="null"/> after recording why the row can't be imported. Every row is
    /// <paramref name="contentTypeDefinition"/> when it is not given.
    /// </param>
    internal static async Task ImportExistingRowsAsync(
        ContentTransferEntry entry,
        DataTable dataTable,
        Dictionary<string, KeyValuePair<int, DataRow>> existingRows,
        ContentTypeDefinition contentTypeDefinition,
        IContentManager contentManager,
        IContentImportManager contentImportManager,
        ImportFileProcessStatsPart progressPart,
        ImportRunMonitor monitor,
        IStringLocalizer localizer,
        ImportRunTelemetry runTelemetry,
        Func<int, DataRow, Task<ContentTypeDefinition>> rowDefinitionAsync = null)
    {
        if (existingRows.Count == 0)
        {
            return;
        }

        // Each row's content type is known before any item is read, so an item is checked against the type its own
        // row names. A row whose type can't be imported already has its error and is left out.
        var rowDefinitions = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var existingRow in existingRows)
        {
            var rowDefinition = rowDefinitionAsync is null
                ? contentTypeDefinition
                : await rowDefinitionAsync(existingRow.Value.Key, existingRow.Value.Value);

            if (rowDefinition is not null)
            {
                rowDefinitions[existingRow.Key] = rowDefinition;
            }
        }

        if (rowDefinitions.Count == 0)
        {
            return;
        }

        var refusedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var publishedIds = new List<string>();
        var existingContentItems = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in await contentManager.GetAsync(rowDefinitions.Keys, VersionOptions.Latest))
        {
            if (!rowDefinitions.TryGetValue(item.ContentItemId, out var itemRowDefinition) ||
                !IsSameContentType(item, itemRowDefinition.Name))
            {
                refusedIds.Add(item.ContentItemId);
            }
            else if (item.Published)
            {
                publishedIds.Add(item.ContentItemId);
            }
            else
            {
                // The latest version is already a draft, and it is the one to edit. Orchard's DraftRequired read
                // returns only the items that were published, so asking it for this one would return nothing and the
                // row would create a second item with the same id.
                existingContentItems.TryAdd(item.ContentItemId, item);
            }
        }

        if (publishedIds.Count > 0)
        {
            foreach (var item in await contentManager.GetAsync(publishedIds, VersionOptions.DraftRequired))
            {
                existingContentItems.TryAdd(item.ContentItemId, item);
            }
        }

        foreach (var existingRow in existingRows)
        {
            if (!rowDefinitions.TryGetValue(existingRow.Key, out var rowDefinition))
            {
                continue;
            }

            monitor.BeginRow(existingRow.Value.Key);

            try
            {
                if (refusedIds.Contains(existingRow.Key))
                {
                    // An id in the file can name any item on the site. A row is imported as one content type the
                    // uploader may import, so a row that names an item of another type (a page, an order) is refused
                    // instead of overwriting it.
                    AddRowError(progressPart, existingRow.Value.Key, localizer["The content item '{0}' is not a {1}.", existingRow.Key, rowDefinition.Name], localizer["Could not be imported"]);
                    runTelemetry.AddError(ContentTransferDiagnostics.ReasonValidation);

                    continue;
                }

                var isNew = !existingContentItems.TryGetValue(existingRow.Key, out var contentItem);

                if (isNew)
                {
                    contentItem = await contentManager.NewAsync(rowDefinition.Name);
                }

                await ProcessRowAsync(
                    entry,
                    contentItem,
                    existingRow.Value.Key,
                    existingRow.Value.Value,
                    dataTable.Columns,
                    rowDefinition,
                    contentManager,
                    contentImportManager,
                    progressPart,
                    localizer,
                    runTelemetry,
                    isNew);
            }
            finally
            {
                monitor.EndRow();
            }
        }
    }
}
