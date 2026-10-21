using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// The setup guide's first product. A site with no product type yet gets one with the parts a product page needs,
/// then the owner adds a product of it.
/// </summary>
internal sealed class ProductsSetupStep : IStoreSetupStep
{
    /// <summary>
    /// The content type the quick action adds.
    /// </summary>
    internal const string ProductContentType = "Product";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ProductContentTypes _productTypes;
    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ProductsSetupStep(
        IContentDefinitionManager contentDefinitionManager,
        ProductContentTypes productTypes,
        ISession session,
        ISiteService siteService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ProductsSetupStep> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _productTypes = productTypes;
        _session = session;
        _siteService = siteService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public string Name
        => "products";

    public int Order
        => 60;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var types = await _productTypes.GetProductTypesAsync(_contentDefinitionManager);
        var count = types.Count == 0
            ? 0
            : await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Published).CountAsync(cancellationToken);
        var type = types.Contains(ProductContentType, StringComparer.Ordinal) ? ProductContentType : (types.Count > 0 ? types[0] : null);

        return new StoreSetupStepDescription
        {
            Title = S["Add your first product"],
            Description = types.Count == 0
                ? S["Products are content items with a price. Add a Product type first, with a title, a description, images and a price, then add your first product of it."]
                : S["Add a product with its name, price, description and pictures, and publish it. It's on sale as soon as it's published."],
            IsDone = count > 0,
            Status = count == 0 ? null : S.Plural(count, "1 product is on sale.", "{0} products are on sale."),
            Url = type is null ? null : _linkGenerator.GetPathByName(_httpContextAccessor.HttpContext, "CreateContentItem", new { id = type }),
            UrlText = S["Add a product"],
            ActionText = types.Count == 0 ? S["Add the Product type"] : null,
        };
    }

    public async Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
    {
        if ((await _productTypes.GetProductTypesAsync(_contentDefinitionManager)).Count > 0)
        {
            return StoreSetupActionResult.Success(S["There's a product type already. Add a product of it."]);
        }

        // The store's currency, read by name so products don't depend on checkout.
        var site = await _siteService.GetSiteSettingsAsync();
        var currency = site.Properties["CheckoutSettings"]?["Currency"]?.GetValue<string>() ?? "USD";

        await _contentDefinitionManager.AlterPartDefinitionAsync(ProductContentType, part => part
            .WithField("Images", field => field
                .OfType("MediaField")
                .WithDisplayName("Images")
                .WithPosition("0")));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(ProductContentType, type => type
            .WithDisplayName("Product")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "products/{{ ContentItem | display_text | slugify }}",
                }))
            .WithPart(nameof(ProductPart), part => part
                .WithPosition("2")
                .WithSettings(new ProductPartSettings
                {
                    Type = ProductType.Good,
                    DefaultCurrency = currency,
                    SalesMode = ProductSalesMode.Purchasable,
                    PriceDisplay = ProductPriceDisplay.Show,
                })
                .WithSettings(new ProductPageSettings { ShowDetails = true }))
            .WithPart(ProductContentType, part => part.WithPosition("3"))
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("4")
                .WithDisplayName("Description")));

        return StoreSetupActionResult.Success(S["Added the Product type. Now add your first product."]);
    }
}
