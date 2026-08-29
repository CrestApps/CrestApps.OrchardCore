using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Drivers;

/// <summary>
/// Adds an informational section to the Web AI data source editor explaining that the sites to scrape are
/// managed as separate web crawlers. The Web source itself has no connection settings.
/// </summary>
internal sealed class WebAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(dataSource.Source, AIDataSourceSourceTypes.Web, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return View("WebAIDataSource_Edit", dataSource).Location("Content:1");
    }
}
