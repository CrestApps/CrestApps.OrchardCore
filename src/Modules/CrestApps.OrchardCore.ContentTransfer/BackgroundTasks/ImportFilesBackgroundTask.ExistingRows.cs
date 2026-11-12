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
    /// Imports the rows of a batch that carry the id of an item. A row whose id names an existing item of the import's
    /// content type updates it; a row whose id names nothing creates a new item; a row whose id names an item of another
    /// type is refused and leaves that item exactly as it was.
    /// </summary>
    /// <remarks>
    /// The items are first read with <see cref="VersionOptions.Latest"/>, which changes nothing. Only the items of the
    /// import's type are then asked for with <see cref="VersionOptions.DraftRequired"/>, which unpublishes an item whose
    /// type is not versionable and adds a draft to one whose type is: asking for every named item that way changed the
    /// items of the rows it then refused. Ids are matched without regard to case, as a case-insensitive database
    /// collation matches them, so an id in another case can't pass for a new item and take the existing one's id.
    /// </remarks>
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
        ImportRunTelemetry runTelemetry)
    {
        if (existingRows.Count == 0)
        {
            return;
        }

        var refusedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var acceptedIds = new List<string>();

        foreach (var item in await contentManager.GetAsync(existingRows.Keys, VersionOptions.Latest))
        {
            if (IsSameContentType(item, entry))
            {
                acceptedIds.Add(item.ContentItemId);
            }
            else
            {
                refusedIds.Add(item.ContentItemId);
            }
        }

        var existingContentItems = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);

        if (acceptedIds.Count > 0)
        {
            foreach (var item in await contentManager.GetAsync(acceptedIds, VersionOptions.DraftRequired))
            {
                existingContentItems.TryAdd(item.ContentItemId, item);
            }
        }

        foreach (var existingRow in existingRows)
        {
            monitor.BeginRow(existingRow.Value.Key);

            try
            {
                if (refusedIds.Contains(existingRow.Key))
                {
                    // An id in the file can name any item on the site. An import is for one content type, and the
                    // uploader's permission is for that type only, so a row that names an item of another type (a
                    // page, an order) is refused instead of overwriting it.
                    AddRowError(progressPart, existingRow.Value.Key, localizer["The content item '{0}' is not a {1}.", existingRow.Key, entry.ContentType], localizer["Could not be imported"]);
                    runTelemetry.AddError(ContentTransferDiagnostics.ReasonValidation);

                    continue;
                }

                var isNew = !existingContentItems.TryGetValue(existingRow.Key, out var contentItem);

                if (isNew)
                {
                    contentItem = await contentManager.NewAsync(entry.ContentType);
                }

                await ProcessRowAsync(
                    entry,
                    contentItem,
                    existingRow.Value.Key,
                    existingRow.Value.Value,
                    dataTable.Columns,
                    contentTypeDefinition,
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
