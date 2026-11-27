using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Drivers;

/// <summary>
/// Adds an informational section to the Web AI data source editor explaining that the sites to scrape are
/// managed as separate web crawlers. The Web source itself has no connection settings.
/// </summary>
internal sealed class WebAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebAIDataSourceDisplayDriver(IStringLocalizer<WebAIDataSourceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(dataSource.Source, AIDataSourceSourceTypes.Web, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return View("WebAIDataSource_Edit", dataSource).Location(l => l.Zone("Content", "1").Card(S["Source"], "20"));
    }
}
