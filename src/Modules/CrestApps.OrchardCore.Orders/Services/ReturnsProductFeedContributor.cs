using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Gives product feed items the store's returns (Settings › Commerce › Returns): goods can be returned within the
/// window, and exchanged when the store allows it; downloads and services have nothing to send back.
/// </summary>
internal sealed class ReturnsProductFeedContributor : IProductFeedItemContributor
{
    private readonly ISiteService _siteService;

    public ReturnsProductFeedContributor(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task ContributeAsync(ProductFeedItemContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = await _siteService.GetSettingsAsync<ReturnSettings>();

        Apply(context.Item, context.ProductType, settings);
    }

    /// <summary>
    /// Sets an item's returns from the store's return settings.
    /// </summary>
    internal static void Apply(ProductFeedItem item, ProductType productType, ReturnSettings settings)
    {
        // The same goods the order's returns cover: downloads and services have nothing to send back.
        var returnable = productType is ProductType.Good or ProductType.Undefined && !item.IsDigital;
        var days = Math.Max(0, settings?.WindowDays ?? 0);

        item.AcceptsReturns = returnable && days > 0;
        item.ReturnWindowDays = item.AcceptsReturns == true ? days : null;
        item.AcceptsExchanges = item.AcceptsReturns == true && settings.AllowExchanges;
    }
}
