using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Settings › Commerce › Product feeds: the defaults for products that say nothing, the feed's key, the feeds'
/// addresses, and what Merchant Center would refuse.
/// </summary>
public sealed class ProductFeedSettingsDisplayDriver : SiteDisplayDriver<ProductFeedSettings>
{
    public const string GroupId = "product-feeds";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ProductFeedService _feeds;

    internal readonly IStringLocalizer S;

    public ProductFeedSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ProductFeedService feeds,
        IStringLocalizer<ProductFeedSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _feeds = feeds;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, ProductFeedSettings settings, BuildEditorContext context)
        => Initialize<ProductFeedSettingsViewModel>("ProductFeedSettings_Edit", async model =>
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = site.BaseUrl?.TrimEnd('/') ?? (request is null ? null : $"{request.Scheme}://{request.Host}{request.PathBase}");
            var query = string.IsNullOrEmpty(settings.Key) ? string.Empty : "?key=" + Uri.EscapeDataString(settings.Key);

            model.DefaultBrand = settings.DefaultBrand;
            model.DefaultCondition = settings.DefaultCondition;
            model.DefaultGoogleProductCategory = settings.DefaultGoogleProductCategory;
            model.IncludeOutOfStock = settings.IncludeOutOfStock;
            model.HasKey = !string.IsNullOrEmpty(settings.Key);
            model.HasBaseUrl = !string.IsNullOrWhiteSpace(site.BaseUrl);
            model.XmlUrl = $"{baseUrl}/feeds/products.xml{query}";
            model.CsvUrl = $"{baseUrl}/feeds/products.csv{query}";
            model.Feed = await _feeds.GetAsync(currency: null, fresh: true);
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ProductFeedPermissions.ManageProductFeeds))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ProductFeedSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ProductFeedPermissions.ManageProductFeeds))
        {
            return null;
        }

        var model = new ProductFeedSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DefaultBrand = string.IsNullOrWhiteSpace(model.DefaultBrand) ? null : model.DefaultBrand.Trim();
        settings.DefaultCondition = model.DefaultCondition is "new" or "refurbished" or "used" ? model.DefaultCondition : "new";
        settings.DefaultGoogleProductCategory = string.IsNullOrWhiteSpace(model.DefaultGoogleProductCategory) ? null : model.DefaultGoogleProductCategory.Trim();
        settings.IncludeOutOfStock = model.IncludeOutOfStock;

        if (model.KeyAction == "new")
        {
            settings.Key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }
        else if (model.KeyAction == "remove")
        {
            settings.Key = null;
        }

        return Edit(site, settings, context);
    }
}
