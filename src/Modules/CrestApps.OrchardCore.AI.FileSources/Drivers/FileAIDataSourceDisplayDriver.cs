using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.FileSources.Drivers;

/// <summary>
/// Adds an informational section to the File AI data source editor explaining that the folders and servers
/// to read are managed as separate file sources. The File source itself has no connection settings.
/// </summary>
internal sealed class FileAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FileAIDataSourceDisplayDriver(IStringLocalizer<FileAIDataSourceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(dataSource.Source, AIDataSourceSourceTypes.File, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return View("FileAIDataSource_Edit", dataSource).Location(l => l.Zone("Content", "1").Card(S["Source"], "20"));
    }
}
