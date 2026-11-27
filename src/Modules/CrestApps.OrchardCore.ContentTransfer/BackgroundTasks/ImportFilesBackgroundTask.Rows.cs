using System.Data;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

public sealed partial class ImportFilesBackgroundTask
{
    /// <summary>
    /// Maps a row onto an item, validates it and saves it. Nothing is saved when a handler can't import the row or the
    /// item doesn't pass validation.
    /// </summary>
    /// <param name="versionToSaveAsync">
    /// Given the mapped item once it passed, returns the version to save: the draft of a published item the row was
    /// mapped onto a copy of. Asked for only after the row passed, so a row that fails leaves the item as it was.
    /// </param>
    internal static async Task ProcessRowAsync(
        ContentTransferEntry entry,
        ContentItem contentItem,
        int rowIndex,
        DataRow row,
        DataColumnCollection columns,
        ContentTypeDefinition contentTypeDefinition,
        IContentManager contentManager,
        IContentImportManager contentImportManager,
        ImportFileProcessStatsPart progressPart,
        IStringLocalizer localizer,
        ImportRunTelemetry runTelemetry,
        bool isNew,
        Func<ContentItem, Task<ContentItem>> versionToSaveAsync = null)
    {
        try
        {
            var importOptions = entry.GetOrCreate<ImportContentOptionsPart>();
            var mapContext = new ContentImportContext()
            {
                ContentItem = contentItem,
                ContentTypeDefinition = contentTypeDefinition,
                Entry = entry,
                Columns = columns,
                Row = row,
            };

            await contentImportManager.ImportAsync(mapContext);

            // A handler that cannot import the row says why, and nothing is saved.
            if (mapContext.Errors.Count > 0)
            {
                AddRowError(progressPart, rowIndex, string.Join(" ", mapContext.Errors), mapContext.ErrorCategory ?? localizer["Could not be imported"]);
                runTelemetry.AddError(ContentTransferDiagnostics.ReasonValidation);

                return;
            }

            var validationResult = await contentManager.ValidateAsync(mapContext.ContentItem);

            if (!validationResult.Succeeded)
            {
                AddRowError(progressPart, rowIndex, FormatValidationErrors(validationResult), localizer["Did not pass validation"]);
                runTelemetry.AddError(ContentTransferDiagnostics.ReasonValidation);

                return;
            }

            ApplyOwnership(mapContext.ContentItem, entry, isNew);

            // The row was mapped onto a copy of a published item: only now that it passed is the item given its draft.
            if (versionToSaveAsync is not null)
            {
                mapContext.ContentItem = await versionToSaveAsync(mapContext.ContentItem);
            }

            if (isNew)
            {
                await contentManager.CreateAsync(mapContext.ContentItem, VersionOptions.DraftRequired);

                if (importOptions.PublishImportedContent)
                {
                    await contentManager.PublishAsync(mapContext.ContentItem);
                }
            }
            else
            {
                await contentManager.UpdateAsync(mapContext.ContentItem);

                if (importOptions.PublishImportedContent)
                {
                    await contentManager.PublishAsync(mapContext.ContentItem);
                }
                else
                {
                    await contentManager.SaveDraftAsync(mapContext.ContentItem);
                }
            }

            progressPart.ImportedCount++;
            runTelemetry.AddRow(ContentTransferDiagnostics.ResultImported);

            RemoveRowError(progressPart, rowIndex);
        }
        catch (Exception ex)
        {
            AddRowError(progressPart, rowIndex, ex.Message, localizer["Could not be imported"]);
            runTelemetry.AddRowException(rowIndex, ex);
        }
    }
}
