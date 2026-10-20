using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Registers product feeds: the store's products as Google Merchant Center XML and as CSV, which Meta and TikTok
/// catalogs read too, and the Product Feed part for brands and identifiers.
/// </summary>
[Feature(ProductConstants.Feature.Feeds)]
public sealed class ProductFeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ProductFeedBuilder>();
        services.AddScoped<ProductFeedService>();
        services.AddDataMigration<Migrations.ProductFeedMigrations>();
        services.AddContentPart<ProductFeedPart>()
            .UseDisplayDriver<ProductFeedPartDisplayDriver>();
        services.AddSiteDisplayDriver<ProductFeedSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(ProductFeedSettingsDisplayDriver.GroupId, ProductFeedPermissions.ManageProductFeeds);
        services.AddPermissionProvider<ProductFeedPermissionProvider>();
        services.AddNavigationProvider<ProductFeedsAdminMenu>();
    }
}

public static class ProductFeedPermissions
{
    public static readonly Permission ManageProductFeeds = new("ManageProductFeeds", "Manage product feeds");
}

internal sealed class ProductFeedPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _all = [ProductFeedPermissions.ManageProductFeeds];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_all);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [new PermissionStereotype { Name = OrchardCoreConstants.Roles.Administrator, Permissions = _all }];
}

/// <summary>
/// Builds a feed once in 15 minutes for each currency, and checks a feed's key.
/// </summary>
public sealed class ProductFeedService
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);

    private readonly ProductFeedBuilder _builder;
    private readonly IMemoryCache _cache;
    private readonly ShellSettings _shellSettings;
    private readonly ISiteService _siteService;

    public ProductFeedService(ProductFeedBuilder builder, IMemoryCache cache, ShellSettings shellSettings, ISiteService siteService)
    {
        _builder = builder;
        _cache = cache;
        _shellSettings = shellSettings;
        _siteService = siteService;
    }

    public async Task<ProductFeed> GetAsync(string currency, bool fresh = false, CancellationToken cancellationToken = default)
    {
        var key = $"ProductFeed:{_shellSettings.Name}:{currency?.ToUpperInvariant()}";

        if (!fresh && _cache.TryGetValue(key, out ProductFeed cached))
        {
            return cached;
        }

        var feed = await _builder.BuildAsync(currency, cancellationToken);

        _cache.Set(key, feed, _lifetime);

        return feed;
    }

    /// <summary>
    /// Returns whether a request may read the feed: it's public, or the request carries its key.
    /// </summary>
    public async Task<bool> IsAllowedAsync(string key)
    {
        var settings = await _siteService.GetSettingsAsync<ProductFeedSettings>();

        return string.IsNullOrEmpty(settings.Key) ||
            (!string.IsNullOrEmpty(key) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(settings.Key)));
    }
}

/// <summary>
/// The feeds a shopping channel fetches: <c>feeds/products.xml</c> (Google Merchant Center, Meta, TikTok) and
/// <c>feeds/products.csv</c>, in the store's currency or <c>?currency=EUR</c>, with <c>?key=</c> when the feed has one.
/// </summary>
[Feature(ProductConstants.Feature.Feeds)]
public sealed class ProductFeedController : Controller
{
    private readonly ProductFeedService _feeds;
    private readonly ISiteService _siteService;

    public ProductFeedController(ProductFeedService feeds, ISiteService siteService)
    {
        _feeds = feeds;
        _siteService = siteService;
    }

    [HttpGet("feeds/products.xml")]
    public async Task<IActionResult> Xml(string currency, string key)
    {
        if (!await _feeds.IsAllowedAsync(key))
        {
            return NotFound();
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var feed = await _feeds.GetAsync(Currency(currency), cancellationToken: HttpContext.RequestAborted);

        return Content(ProductFeedWriter.ToXml(feed, site.SiteName, site.BaseUrl ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"), "application/xml; charset=utf-8");
    }

    [HttpGet("feeds/products.csv")]
    public async Task<IActionResult> Csv(string currency, string key)
    {
        if (!await _feeds.IsAllowedAsync(key))
        {
            return NotFound();
        }

        var feed = await _feeds.GetAsync(Currency(currency), cancellationToken: HttpContext.RequestAborted);

        return File(Encoding.UTF8.GetBytes(ProductFeedWriter.ToCsv(feed)), "text/csv; charset=utf-8", "products.csv");
    }

    private static string Currency(string currency)
        => currency is { Length: 3 } && currency.All(char.IsAsciiLetter) ? currency.ToUpperInvariant() : null;
}

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

    public ProductFeedSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, ProductFeedService feeds, IStringLocalizer<ProductFeedSettingsDisplayDriver> stringLocalizer)
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

public class ProductFeedSettingsViewModel
{
    public string DefaultBrand { get; set; }

    public string DefaultCondition { get; set; } = "new";

    public string DefaultGoogleProductCategory { get; set; }

    public bool IncludeOutOfStock { get; set; } = true;

    /// <summary>
    /// Gets or sets <c>new</c> to make a new key, <c>remove</c> to make the feed public, or none to keep it.
    /// </summary>
    public string KeyAction { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool HasKey { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool HasBaseUrl { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string XmlUrl { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string CsvUrl { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public ProductFeed Feed { get; set; }
}

/// <summary>
/// The Product Feed part's editor: brand, GTIN, MPN, condition and Google category, or staying out of feeds.
/// </summary>
internal sealed class ProductFeedPartDisplayDriver : ContentPartDisplayDriver<ProductFeedPart>
{
    internal readonly IStringLocalizer S;

    public ProductFeedPartDisplayDriver(IStringLocalizer<ProductFeedPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ProductFeedPart part, BuildPartEditorContext context)
        => Initialize<ProductFeedPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Brand = part.Brand;
            model.Gtin = part.Gtin;
            model.Mpn = part.Mpn;
            model.Condition = part.Condition;
            model.GoogleProductCategory = part.GoogleProductCategory;
            model.ExcludeFromFeeds = part.ExcludeFromFeeds;
        });

    public override async Task<IDisplayResult> UpdateAsync(ProductFeedPart part, UpdatePartEditorContext context)
    {
        var model = new ProductFeedPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrWhiteSpace(model.Gtin) && !ProductFeedValidator.IsValidGtin(model.Gtin))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Gtin), S["Enter a GTIN of 8, 12, 13 or 14 digits with its check digit, such as the barcode's number."]);
        }

        part.Brand = Clean(model.Brand, 70);
        part.Gtin = Clean(model.Gtin, 14);
        part.Mpn = Clean(model.Mpn, 70);
        part.Condition = model.Condition is "new" or "refurbished" or "used" ? model.Condition : null;
        part.GoogleProductCategory = Clean(model.GoogleProductCategory, 250);
        part.ExcludeFromFeeds = model.ExcludeFromFeeds;

        return Edit(part, context);
    }

    private static string Clean(string value, int length)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, length)];
}

public class ProductFeedPartViewModel
{
    public string Brand { get; set; }

    public string Gtin { get; set; }

    public string Mpn { get; set; }

    public string Condition { get; set; }

    public string GoogleProductCategory { get; set; }

    public bool ExcludeFromFeeds { get; set; }
}

/// <summary>
/// Settings › Commerce › Product feeds.
/// </summary>
internal sealed class ProductFeedsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ProductFeedsAdminMenu(IStringLocalizer<ProductFeedsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Product feeds"], S["Product feeds"].PrefixPosition(), feeds => feeds
                        .AddClass("product-feeds-settings")
                        .Id("productFeedsSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = ProductFeedSettingsDisplayDriver.GroupId })
                        .Permission(ProductFeedPermissions.ManageProductFeeds)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
