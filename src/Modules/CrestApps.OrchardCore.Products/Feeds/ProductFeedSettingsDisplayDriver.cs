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
    {
        return Combine(
            Initialize<ProductFeedSettingsViewModel>("ProductFeedSettings_Edit", model => FillAsync(model, site, settings, includeFeed: true))
                .Location(l => l.Zone("Content", "1").Card(S["Feed Addresses"], "10"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ProductFeedPermissions.ManageProductFeeds))
                .OnGroup(SettingsGroupId),
            Initialize<ProductFeedSettingsViewModel>("ProductFeedSettingsDefaults_Edit", model => FillAsync(model, site, settings, includeFeed: false))
                .Location(l => l.Zone("Content", "2").Card(S["Feed Defaults"], "20"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ProductFeedPermissions.ManageProductFeeds))
                .OnGroup(SettingsGroupId),
            Initialize<ProductFeedSettingsViewModel>("ProductFeedSettingsAgents_Edit", model => FillAsync(model, site, settings, includeFeed: false))
                .Location(l => l.Zone("Content", "3").Card(S["AI Agents"], "30"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ProductFeedPermissions.ManageProductFeeds))
                .OnGroup(SettingsGroupId),
            Initialize<ProductFeedSettingsViewModel>("ProductFeedSettingsAccess_Edit", model => FillAsync(model, site, settings, includeFeed: false))
                .Location(l => l.Zone("Content", "4").Card(S["Access"], "40"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ProductFeedPermissions.ManageProductFeeds))
                .OnGroup(SettingsGroupId));
    }

    // Every card of the page binds the same view model; only the first one shows the generated feed, so the others skip building it.
    private async ValueTask FillAsync(ProductFeedSettingsViewModel model, ISite site, ProductFeedSettings settings, bool includeFeed)
    {
        model.DefaultBrand = settings.DefaultBrand;
        model.DefaultCondition = settings.DefaultCondition;
        model.DefaultGoogleProductCategory = settings.DefaultGoogleProductCategory;
        model.IncludeOutOfStock = settings.IncludeOutOfStock;
        model.ShowToAgentsByDefault = settings.ShowToAgentsByDefault;
        model.AgentCheckoutByDefault = settings.AgentCheckoutByDefault;
        model.HasKey = !string.IsNullOrEmpty(settings.Key);
        model.HasBaseUrl = !string.IsNullOrWhiteSpace(site.BaseUrl);

        if (!includeFeed)
        {
            return;
        }

        var baseUrl = ProductFeedAddresses.GetBaseUrl(site, _httpContextAccessor.HttpContext?.Request);

        model.XmlUrl = ProductFeedAddresses.Build(baseUrl, ProductFeedAddresses.XmlPath, settings.Key);
        model.CsvUrl = ProductFeedAddresses.Build(baseUrl, ProductFeedAddresses.CsvPath, settings.Key);
        model.Feed = await _feeds.GetAsync(currency: null, fresh: true);
    }

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
