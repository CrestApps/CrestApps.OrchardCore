using CrestApps.Core.AI;
using CrestApps.OrchardCore.Products.AI;
using CrestApps.OrchardCore.Products.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products;

/// <summary>
/// Registers the product and currency AI tools whenever the Products and AI features are on. Listing products needs
/// <c>ListContent</c> and changing one needs <c>EditContent</c> on it (and <c>PublishContent</c> to publish), as the
/// content editor does; the currency tools need <c>ManageCurrencies</c>, and deleting a currency asks for approval.
/// </summary>
[Feature(ProductConstants.Feature.ModuleId)]
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
        services.AddScoped<CurrencyToolScope>();

        services.AddCoreAITool<ListProductsTool>(ListProductsTool.TheName)
            .WithTitle(S["List Products"])
            .WithDescription(S["Lists products (content items whose type has the Product part) with content item id, type, SKU, price and currency, sale price and dates, variant count, published state and, when Inventory is on, units available. Filters by text, exact SKU of a product or variant, content type, on sale, and published or draft, with paging; use it to find a product before editing it, its price or its stock."])
            .WithCategory(S["Products"])
            .Selectable();

        services.AddCoreAITool<SaveProductVariantsTool>(SaveProductVariantsTool.TheName)
            .WithTitle(S["Save Product Variants"])
            .WithDescription(S["Sets a product's options (for example Size and Color) and its variants' SKU, price, compare-at price, sale price, image and enabled flag. Variants are generated from the options and existing variant ids are kept, so stock and past orders keep pointing at them; it refuses a SKU used by two variants and saves a draft unless told to publish."])
            .WithCategory(S["Products"])
            .Selectable();

        services.AddCoreAITool<UpdateProductPricingTool>(UpdateProductPricingTool.TheName)
            .WithTitle(S["Update Product Pricing"])
            .WithDescription(S["Changes the price fields of one product, or of one of its variants: price, compare-at price, sale price and the sale's start and end in the site's time zone, with the same checks as the product editor. Use it for price changes and scheduled sales; use the content tools for everything else on a product."])
            .WithCategory(S["Products"])
            .Selectable();

        services.AddCoreAITool<ListCurrenciesTool>(ListCurrenciesTool.TheName)
            .WithTitle(S["List Currencies"])
            .WithDescription(S["Lists the currencies the store can price products in, with ISO code and display name, optionally filtered by text."])
            .WithCategory(S["Products"])
            .Selectable();

        services.AddCoreAITool<SaveCurrencyTool>(SaveCurrencyTool.TheName)
            .WithTitle(S["Save Currency"])
            .WithDescription(S["Adds a currency by three-letter ISO 4217 code and display name, or renames an existing one."])
            .WithCategory(S["Products"])
            .Selectable();

        services.AddCoreAITool<DeleteCurrencyTool>(DeleteCurrencyTool.TheName)
            .WithTitle(S["Delete Currency"])
            .WithDescription(S["Deletes a currency. It is refused while any product is priced in it; the products are named so their prices can be changed first."])
            .WithCategory(S["Products"])
            .RequireApproval()
            .Selectable();
    }
}

/// <summary>
/// Registers the product feed AI tools whenever the Product Feeds and AI features are on. They need
/// <c>ManageProductFeeds</c>; making or removing the feed's access key asks for approval.
/// </summary>
[Feature(ProductConstants.Feature.Feeds)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class FeedsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FeedsAIToolsStartup(IStringLocalizer<FeedsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ProductFeedToolScope>();

        services.AddCoreAITool<GetProductFeedStatusTool>(GetProductFeedStatusTool.TheName)
            .WithTitle(S["Get Product Feed Status"])
            .WithDescription(S["Returns the product feed's XML and CSV addresses, whether an access key and the site base URL are set, how many items the feed holds, and the problems found (which product, what Merchant Center would refuse, whether it was left out). Use it to check a feed before submitting it to Google, Meta or TikTok. The access key is never shown."])
            .WithCategory(S["Products"])
            .Selectable();

        services.AddCoreAITool<RegenerateProductFeedKeyTool>(RegenerateProductFeedKeyTool.TheName)
            .WithTitle(S["Regenerate Product Feed Key"])
            .WithDescription(S["Makes a new access key for the product feed addresses, or removes the key so the feed is public. A new key stops every feed address already given to a shopping channel from working until it is updated there. The key itself is never shown; the settings page shows the new addresses."])
            .WithCategory(S["Products"])
            .RequireApproval()
            .Selectable();
    }
}
