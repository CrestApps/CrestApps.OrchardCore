using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CrestApps.Core.AI;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Products.AI;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products;

/// <summary>
/// Registers the product AI tools whenever the Products and AI features are on, and exposes the currencies to the
/// generic record tools (<c>listRecords</c>, <c>getRecord</c>, <c>saveRecord</c> and <c>deleteRecord</c>) as the
/// <c>currency</c> kind. Listing products needs <c>ListContent</c> and changing one needs <c>EditContent</c> on it (and
/// <c>PublishContent</c> to publish), as the content editor does. The currency kind needs <c>ManageCurrencies</c>, as
/// Commerce › Currencies; its writes replay the Currencies editor, with the page's refusal of a code that already exists
/// and of deleting a currency products are priced in.
/// </summary>
[Feature(ProductConstants.Feature.ModuleId)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    // How many products a refused delete names, as the Currencies page names a few.
    private const int MaxNamedProducts = 10;

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
        services.AddAIRecordKind<CurrencyEntry>("currency", kind => kind
            .WithTitle(S["Currency"])
            .WithDescription(S["a currency the store can price products in: its three-letter ISO 4217 code (Name, such as EUR, set when the currency is created) and its display name; a currency products are priced in is not deleted"])
            .WithPermission(ProductsConstants.Permissions.ManageCurrencies)
            .UseCatalogManager<INamedCatalogManager<CurrencyEntry>>()
            .WithProjection(currency => new
            {
                id = currency.ItemId,
                code = currency.Name,
                displayName = currency.DisplayName,
            })
            .ValidateUsing(ValidateCurrencyAsync)
            .WithInUseCheck(GetCurrencyUseAsync)
            .UseEditorReplay());

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
    }

    // The handler's rules (a three-letter ISO 4217 code and a display name), then the check the Currencies page makes
    // after it binds the editor and before it saves: no other currency has the same code.
    private static async ValueTask<ValidationResultDetails> ValidateCurrencyAsync(IServiceProvider services, CurrencyEntry currency, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<INamedCatalogManager<CurrencyEntry>>();
        var result = await manager.ValidateAsync(currency, cancellationToken);

        if (string.IsNullOrWhiteSpace(currency.Name))
        {
            return result;
        }

        var existing = await manager.FindByNameAsync(currency.Name, cancellationToken);

        if (existing is not null && !string.Equals(existing.ItemId, currency.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            result.Fail(new ValidationResult("A currency with the same code already exists.", [nameof(CurrencyEntry.Name)]));
        }

        return result;
    }

    // As the Currencies page does before it deletes a currency: one that products are priced in is kept, and the
    // products are named so their prices can be changed first.
    private static async ValueTask<string> GetCurrencyUseAsync(IServiceProvider services, CurrencyEntry currency, CancellationToken cancellationToken)
    {
        var products = await services.GetRequiredService<CurrencyUsageService>().GetProductsUsingAsync(currency.Name);

        if (products.Count == 0)
        {
            return null;
        }

        var named = string.Join(", ", products.Take(MaxNamedProducts));

        return products.Count > MaxNamedProducts
            ? string.Create(CultureInfo.InvariantCulture, $"Products are priced in {currency.Name}: {named} and {products.Count - MaxNamedProducts} more. Change their prices to another currency first.")
            : $"Products are priced in {currency.Name}: {named}. Change their prices to another currency first.";
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
