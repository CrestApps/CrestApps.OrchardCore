using CrestApps.OrchardCore.Products.Core.Feeds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Settings › Commerce › Product Feeds: the defaults for products that say nothing, the feed's key, the feeds'
/// addresses, and what Merchant Center would refuse.
/// </summary>
public sealed class ProductFeedSettingsDisplayDriver : SiteDisplayDriver<ProductFeedSettings>
{
    public const string GroupId = "product-feeds";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IProductFeedService _feeds;

    internal readonly IStringLocalizer S;

    public ProductFeedSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IProductFeedService feeds,
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
            var baseUrl = ProductFeedAddresses.GetBaseUrl(site, _httpContextAccessor.HttpContext?.Request);

            model.DefaultBrand = settings.DefaultBrand;
            model.DefaultCondition = settings.DefaultCondition;
            model.DefaultGoogleProductCategory = settings.DefaultGoogleProductCategory;
            model.IncludeOutOfStock = settings.IncludeOutOfStock;
            model.ShowToAgentsByDefault = settings.ShowToAgentsByDefault;
            model.AgentCheckoutByDefault = settings.AgentCheckoutByDefault;
            model.HasKey = !string.IsNullOrEmpty(settings.Key);
            model.HasBaseUrl = !string.IsNullOrWhiteSpace(site.BaseUrl);
            model.XmlUrl = ProductFeedAddresses.Build(baseUrl, ProductFeedAddresses.XmlPath, settings.Key);
            model.CsvUrl = ProductFeedAddresses.Build(baseUrl, ProductFeedAddresses.CsvPath, settings.Key);
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
        settings.ShowToAgentsByDefault = model.ShowToAgentsByDefault;
        settings.AgentCheckoutByDefault = model.AgentCheckoutByDefault;

        if (model.KeyAction == "new")
        {
            settings.Key = ProductFeedAddresses.NewKey();
        }
        else if (model.KeyAction == "remove")
        {
            settings.Key = null;
        }

        return Edit(site, settings, context);
    }
}
