namespace CrestApps.OrchardCore.Products.Core;

public static class ProductConstants
{
    /// <summary>
    /// The content type of a product listing page.
    /// </summary>
    public const string ProductListingContentType = "ProductListing";

    public static class Feature
    {
        public const string ModuleId = "CrestApps.OrchardCore.Products";

        /// <summary>
        /// The feature that lists products on the front end, with filters, sorting and product pages.
        /// </summary>
        public const string Listing = "CrestApps.OrchardCore.Products.Listing";

        public const string Recommendations = "CrestApps.OrchardCore.Products.Recommendations";

        public const string Comparison = "CrestApps.OrchardCore.Products.Comparison";

        public const string Grouped = "CrestApps.OrchardCore.Products.Grouped";

        public const string Bundles = "CrestApps.OrchardCore.Products.Bundles";

        /// <summary>
        /// The catalog API: products, priced for the caller, with their variants and stock, under <c>api/commerce/v1</c>.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Products.Api";

    }
}
