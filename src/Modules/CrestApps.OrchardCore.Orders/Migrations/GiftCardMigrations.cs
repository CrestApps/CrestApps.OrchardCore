using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Settings;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Orders.Migrations;

/// <summary>
/// Creates the gift card index, the part that makes a product a gift card, and a Gift card product type to sell them.
/// </summary>
internal sealed class GiftCardMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISiteService _siteService;

    public GiftCardMigrations(IContentDefinitionManager contentDefinitionManager, ISiteService siteService)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _siteService = siteService;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<GiftCardIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("NormalizedCode", column => column.WithLength(32))
            .Column<string>("LastCharacters", column => column.WithLength(8))
            .Column<string>("RecipientEmail", column => column.WithLength(255))
            .Column<decimal>("Balance")
            .Column<string>("Currency", column => column.WithLength(3))
            .Column<bool>("IsEnabled")
            .Column<DateTime>("ExpiresUtc", column => column.Nullable())
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc"),
            collection: OrderConstants.CollectionName
        );

        // A buyer's code is looked up on every apply; staff search by the last characters; an order finds its cards.
        await SchemaBuilder.AlterIndexTableAsync<GiftCardIndex>(table =>
        {
            table.CreateIndex("IDX_GiftCardIndex_Code", "NormalizedCode");
            table.CreateIndex("IDX_GiftCardIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_GiftCardIndex_Last", "LastCharacters");
            table.CreateIndex("IDX_GiftCardIndex_Order", "OrderId");
        }, collection: OrderConstants.CollectionName);

        await _contentDefinitionManager.AlterPartDefinitionAsync("GiftCardPart", part => part
            .Attachable()
            .WithDisplayName("Gift card")
            .WithDescription("Makes a product a gift card: each one bought issues a gift card worth its price.")
        );

        // A product type ready to sell gift cards: digital, so it isn't shipped. Its price is what each card holds, in
        // the store's checkout currency.
        var currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        await _contentDefinitionManager.AlterTypeDefinitionAsync(OrderConstants.GiftCardContentType, type => type
            .WithDisplayName("Gift card")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("ProductPart", part => part
                .WithPosition("1")
                .WithSettings(new ProductPartSettings { Type = ProductType.Digital, DefaultCurrency = string.IsNullOrEmpty(currency) ? "USD" : currency })
                .WithSettings(new ProductPageSettings { ShowDetails = true }))
            .WithPart("GiftCardPart", part => part.WithPosition("2"))
        );

        return 1;
    }
}
