using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Leaves converted leads out of a lead export when the export asks for it.
/// </summary>
public sealed class LeadExportFilterHandler : IContentImportHandler
{
    /// <inheritdoc/>
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentContext context)
        => [];

    /// <inheritdoc/>
    public Task ImportAsync(ContentImportContext content)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ExportAsync(ContentExportContext content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.ContentItem?.TryGet<LeadPart>(out var lead) == true &&
            lead.IsConverted &&
            (content.Entry?.TryGet<LeadExportOptionsPart>(out var options) != true || options.ExcludeConvertedLeads))
        {
            content.Exclude = true;
        }

        return Task.CompletedTask;
    }
}
