using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Adds a Products item to the Commerce admin menu: the content list narrowed to every content type that is a
/// product, so goods, listed items and subscription plans are managed together.
/// </summary>
internal sealed class ProductsAdminMenu : AdminNavigationProvider
{
    private readonly ProductContentTypes _contentTypes;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public ProductsAdminMenu(
        ProductContentTypes contentTypes,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<ProductsAdminMenu> stringLocalizer)
    {
        _contentTypes = contentTypes;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var types = await _contentTypes.GetProductTypesAsync(_contentDefinitionManager);

        if (types.Count == 0)
        {
            return;
        }

        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Products"], S["Products"].PrefixPosition("0.1"), products => products
                    .Id("commerceProducts")
                    .Action("List", "Admin", ContentList(types))
                    .Permission(CommonPermissions.ListContent)
                    .LocalNav()));
    }

    /// <summary>
    /// The route of Orchard Core's content list narrowed to some content types, which it takes as a comma-separated
    /// content type route value. Creating new content from that list offers only those types.
    /// </summary>
    internal static RouteValueDictionary ContentList(IReadOnlyList<string> contentTypes)
        => new()
        {
            { "area", "OrchardCore.Contents" },
            { "contentTypeId", string.Join(',', contentTypes) },
        };
}

/// <summary>
/// Adds a Product Listings item to the Commerce admin menu: the content list narrowed to every content type that
/// is a product listing page.
/// </summary>
internal sealed class ProductListingsAdminMenu : AdminNavigationProvider
{
    private readonly ProductContentTypes _contentTypes;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public ProductListingsAdminMenu(
        ProductContentTypes contentTypes,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<ProductListingsAdminMenu> stringLocalizer)
    {
        _contentTypes = contentTypes;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var types = await _contentTypes.GetListingTypesAsync(_contentDefinitionManager);

        if (types.Count == 0)
        {
            return;
        }

        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Product Listings"], S["Product Listings"].PrefixPosition("0.2"), listings => listings
                    .Id("commerceProductListings")
                    .Action("List", "Admin", ProductsAdminMenu.ContentList(types))
                    .Permission(CommonPermissions.ListContent)
                    .LocalNav()));
    }
}
