using CrestApps.Core.AI;
using CrestApps.OrchardCore.ContentTransfer.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Registers the AI tools that list transferable content types and follow, pause, resume and delete bulk import and
/// export entries whenever the AI feature is on. Deleting an entry asks for approval. Starting an import or an export is
/// done on the Import and Export pages.
/// </summary>
[Feature(ContentTransferConstants.Feature.ModuleId)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListContentTransferTypesTool>(ListContentTransferTypesTool.TheName)
            .WithTitle(S["List Content Transfer Types"])
            .WithDescription(S["Lists the content types the user may bulk import or bulk export, the file formats enabled, and, for one content type, the column names an import file takes and an export file contains. Use it before an import or export to know the columns."])
            .WithCategory(S["Content Transfer"])
            .Selectable();

        services.AddCoreAITool<ListContentTransferEntriesTool>(ListContentTransferEntriesTool.TheName)
            .WithTitle(S["List Content Transfer Entries"])
            .WithDescription(S["Lists bulk import or export entries with content type, file name, status, who started them and when, filtered by direction and status, with paging."])
            .WithCategory(S["Content Transfer"])
            .Selectable();

        services.AddCoreAITool<GetContentTransferEntryTool>(GetContentTransferEntryTool.TheName)
            .WithTitle(S["Get Content Transfer Entry"])
            .WithDescription(S["Returns one import or export entry with its progress: total rows, processed, imported, failed rows with their error messages, and the download link of a completed export. Use it to report progress or explain failures."])
            .WithCategory(S["Content Transfer"])
            .Selectable();

        services.AddCoreAITool<SetContentImportStateTool>(SetContentImportStateTool.TheName)
            .WithTitle(S["Set Content Import State"])
            .WithDescription(S["Pauses a running import or resumes a pending, paused or failed one. A resumed import continues from its last saved row."])
            .WithCategory(S["Content Transfer"])
            .Selectable();

        services.AddCoreAITool<DeleteContentTransferEntryTool>(DeleteContentTransferEntryTool.TheName)
            .WithTitle(S["Delete Content Transfer Entry"])
            .WithDescription(S["Deletes an import or export entry and its stored file in the background. Content items already imported are not removed."])
            .WithCategory(S["Content Transfer"])
            .RequireApproval()
            .Selectable();
    }
}
