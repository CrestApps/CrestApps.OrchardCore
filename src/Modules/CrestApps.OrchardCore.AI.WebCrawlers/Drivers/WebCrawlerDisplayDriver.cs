using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.OrchardCore.AI.WebCrawlers.Services;
using CrestApps.OrchardCore.AI.WebCrawlers.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Drivers;

/// <summary>
/// Display driver for the fields shared by every web-crawler strategy: display name, target Web data
/// source, enabled flag, and re-index interval.
/// </summary>
internal sealed class WebCrawlerDisplayDriver : DisplayDriver<WebCrawler>
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IReadOnlyList<WebCrawlerStrategyDescriptor> _strategies;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawlerDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataSourceStore">The AI data source store.</param>
    /// <param name="strategyOptions">The registered crawl strategies.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebCrawlerDisplayDriver(
        IAIDataSourceStore dataSourceStore,
        IOptions<WebCrawlerStrategyOptions> strategyOptions,
        IStringLocalizer<WebCrawlerDisplayDriver> stringLocalizer)
    {
        _dataSourceStore = dataSourceStore;
        _strategies = strategyOptions.Value.Strategies;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(WebCrawler crawler, BuildDisplayContext context)
    {
        if (!IsCrawler(crawler))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return CombineAsync(
            View("WebCrawler_Fields_SummaryAdmin", crawler).Location("Content:1"),
            View("WebCrawler_Buttons_SummaryAdmin", crawler).Location("Actions:5"),
            View("WebCrawler_DefaultMeta_SummaryAdmin", crawler).Location("Meta:5"),
            View("WebCrawler_ActionsMenu_SummaryAdmin", crawler).Location("ActionsMenu:10")
        );
    }

    public override IDisplayResult Edit(WebCrawler crawler, BuildEditorContext context)
    {
        if (!IsCrawler(crawler))
        {
            return null;
        }

        return Initialize<WebCrawlerFieldsViewModel>("WebCrawlerFields_Edit", async model =>
        {
            model.DisplayText = crawler.DisplayText;
            model.AIDataSourceId = crawler.AIDataSourceId;
            model.Enabled = crawler.Enabled;
            model.ReindexIntervalMinutes = crawler.ReindexIntervalMinutes;

            var dataSources = await _dataSourceStore.GetAsync(AIDataSourceSourceTypes.Web);

            model.DataSources = dataSources
                .OrderBy(dataSource => dataSource.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(dataSource => new SelectListItem(dataSource.DisplayText, dataSource.ItemId))
                .ToArray();

            model.HasDataSources = model.DataSources.Any();
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(WebCrawler crawler, UpdateEditorContext context)
    {
        if (!IsCrawler(crawler))
        {
            return null;
        }

        var model = new WebCrawlerFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.AIDataSourceId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIDataSourceId), S["A target Web data source is required."]);
        }

        if (model.ReindexIntervalMinutes is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ReindexIntervalMinutes), S["The re-index interval must be a positive number of minutes."]);
        }

        crawler.DisplayText = model.DisplayText?.Trim();
        crawler.AIDataSourceId = model.AIDataSourceId?.Trim();
        crawler.Enabled = model.Enabled;
        crawler.ReindexIntervalMinutes = model.ReindexIntervalMinutes;

        return Edit(crawler, context);
    }

    /// <summary>
    /// Determines whether this record is a web crawler rather than a file source, which the same record
    /// type also stores.
    /// </summary>
    /// <param name="crawler">The record.</param>
    /// <returns><see langword="true"/> when its source is a registered crawl strategy.</returns>
    private bool IsCrawler(WebCrawler crawler)
        => WebCrawlerRecords.IsCrawlStrategy(crawler.Source, _strategies);
}
