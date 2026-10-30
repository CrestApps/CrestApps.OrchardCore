---
sidebar_label: Products
sidebar_position: 12
title: Products
description: Attach product pricing and type metadata to any Orchard Core content type, and list products on the front end with filters, sorting, search and product pages.
user_manual:
  - user-manual/commerce/products-and-currencies
  - user-manual/commerce/product-listings
  - user-manual/commerce/headless-api
---

| | |
| --- | --- |
| **Feature Name** | Products |
| **Feature ID** | `CrestApps.OrchardCore.Products` |
| **Category** | Content Management |

This video shows the product screens in the admin:

<YouTube video="products" title="Video overview of products, prices and listings" />

The **Products** module turns any Orchard Core content type into a purchasable product by attaching a reusable **`ProductPart`**. It provides the shared pricing and product-type metadata that other modules — most notably [Subscriptions](subscriptions) — build on top of, without prescribing how a product is sold. It also manages the shared **Commerce → Currencies** catalog that product and subscription editors use for currency dropdowns.

## Overview

Once enabled, the module registers an attachable content part named **Product** (`ProductPart`). Attach it to a content type and every content item of that type gains a **Price** value and the **Currency** that price is sold in, plus a design-time **Type** setting that classifies the product as a *Good*, *Service*, or *Digital* item and a **Default currency** applied when an item does not set its own.

The available currencies come from the managed **Commerce → Currencies** catalog. Editors choose from a friendly dropdown (for example **US Dollar (USD)**) instead of typing free-form codes, which keeps product pricing, product defaults, and subscription checkout settings aligned on the same managed list.

Because the part is a normal Orchard Core content part, products participate in the full CMS pipeline: they can be listed, queried, localized, versioned, indexed for search, and secured with the same permissions and workflows as any other content item.

## The Product part

`ProductPart` exposes these content-item values:

| Property | Type | Description |
| --- | --- | --- |
| `Price` | `decimal?` | The price of the item, expressed in the product's own `Currency`. It is required when the product can be bought or its price is shown, and optional otherwise. |
| `Currency` | `string` | The ISO-4217 currency code the price is sold in (for example `USD`). When empty, the content type's `ProductPartSettings.DefaultCurrency` applies. A product owns its currency. Prices are converted only into a shopper's market currency, by [Markets](markets.md). |
| `Sku` | `string` | An optional stock-keeping unit that uniquely identifies the product for carts, orders, and fulfilment. |
| `CompareAtPrice` | `decimal?` | An optional "was" or list price. When it is higher than `Price`, it is shown struck through with the saving. |
| `IsPurchasable` | `bool` | Whether this item can be bought online. Only used when the type's sales mode is *Per item*. |
| `PriceDisplay` | `ProductPriceDisplay?` | How this item's price is shown. Only used when the type allows a per-item price display. |

The part's behavior is configured per content type through **`ProductPartSettings`**:

| Setting | Type | Description |
| --- | --- | --- |
| `Type` | `ProductType` | Classifies the product. One of `Undefined`, `Good`, `Service`, or `Digital`. |
| `DefaultCurrency` | `string` | The ISO-4217 currency code applied to products of this type when an item does not set its own `Currency`. |
| `SalesMode` | `ProductSalesMode` | Whether products of this type can be bought online: `Purchasable` (the default), `ListingOnly`, or `PerItem`. |
| `PriceDisplay` | `ProductPriceDisplay` | How prices are shown to visitors: `Show` (the default), `OnRequest`, or `Hidden`. |
| `OnRequestLabel` | `string` | The text shown in place of the price when the price display is `OnRequest`, such as *Call for price*. |
| `AllowPriceDisplayOverride` | `bool` | Lets each product choose its own price display. |

## Attaching the Product part

You can attach the part from the admin UI (**Design → Content Definition → Content Types → *your type* → Add Parts → Product**) or in a migration:

```csharp
internal sealed class ProductContentTypeMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductContentTypeMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Membership", type => type
            .Creatable()
            .Listable()
            .WithPart("ProductPart", part => part
                .WithSettings(new ProductPartSettings
                {
                    Type = ProductType.Service,
                })));

        return 1;
    }
}
```

## Product types

The `ProductType` enum lets the editor and downstream modules reason about what is being sold:

| Value | Use it for |
| --- | --- |
| `Undefined` | The default; the product has not been classified. |
| `Good` | A tangible good sold as a one-time purchase. |
| `Service` | A service offering. |
| `Digital` | A digital good or download. |

When the [Taxation](taxation) feature is enabled, the product type maps to a taxable-item kind so the taxation engine can classify what is being sold: `Good` → *Physical*, `Service` → *Service*, `Digital` → *Digital*, and `Undefined` falls back to *Physical*.

## Listed or for sale

Not every product is sold online. A car dealer lists its whole stock but takes payment for only some of it, and a real estate site lists homes it never sells through a checkout. The **sales mode** on the type's Product part settings decides this:

| Sales mode | Effect |
| --- | --- |
| `Purchasable` | Every product of the type can be bought online. This is the default, so existing product types keep selling. |
| `ListingOnly` | Products are shown and browsed but never sold online. |
| `PerItem` | Each product has a **Sell online** switch. |

The rule is enforced on the server, not only in the editor: `IProductSnapshotResolver` returns no snapshot for a product that cannot be bought, so no checkout path can sell it, whatever a request contains.

The **price display** decides what visitors see in place of the price. `OnRequest` shows a label such as *Call for price*, and `Hidden` shows nothing. A listed product whose price is not shown does not need a price at all.

## Managed currencies

The module adds a **Currencies** screen under **Commerce** for managing the currencies that editors may use:

- Each entry stores the **ISO-4217 currency code** (`USD`, `EUR`, `JPY`, and so on).
- Each entry also stores the **friendly display name** shown in dropdowns.
- New tenants are seeded automatically from a migration recipe with a small default list (`USD`, `EUR`, and `GBP`), which you can edit, extend, or trim later.

These managed currencies are reused by:

- `ProductPart.Currency`
- `ProductPartSettings.DefaultCurrency`
- `SubscriptionSettings.Currency`

### Recipes and deployment

The module adds a dedicated **`Currencies`** recipe step and deployment step for the managed currencies catalog.

Recipe example:

```json
{
  "name": "Currencies",
  "Currencies": [
    {
      "Name": "USD",
      "DisplayName": "US Dollar"
    },
    {
      "Name": "EUR",
      "DisplayName": "Euro"
    }
  ]
}
```

## Taxation

Products participate in taxation through the [Taxation](taxation) framework — the Products module never calculates tax itself. When both **Products** and **Taxation** are enabled, add the **Taxation** part to a product content type and mark it taxable. A `ProductTaxableItemProvider` then exposes each product to the taxation engine as an `ITaxableItem`, supplying:

- the price from the **Product** part,
- the taxable-item kind derived from the product type,
- the tax category, classification, and external tax code from the **Taxation** part.

Because taxation is an optional feature dependency, products keep working normally when Taxation is disabled. See the [Taxation](taxation) module for how tax is then determined, snapshotted, and refunded.

## The sellable snapshot

A price on a content item is not enough to sell it: a cart or order must capture *what was purchasable at the moment of purchase* so a later price or definition change never rewrites history. The Products module provides that seam without forcing every consumer to read the content item directly.

- **`ISellableProduct`** is an immutable snapshot of a purchasable product — its content item id and version, content type, SKU, title, unit price (as `decimal`), currency, product type, and tax classification codes.
- **`IProductSnapshotResolver`** resolves an `ISellableProduct` from a `ProductSnapshotContext` (the content item plus the requested currency, quantity, SKU, and variant). The default resolver reads the **Product**, **Product variants** and **Taxation** parts and the `ProductPartSettings`. For a product sold in variants it resolves only an enabled variant named by `VariantId`, and the snapshot carries that variant's SKU, price, `VariantId` and `VariantTitle` (see [Selling a product in variants](#selling-a-product-in-variants)).

Both the editable `ProductPart.Price` and the snapshot's unit price are `decimal`, the authoritative representation for stored financial records, so a price never suffers binary floating-point drift between editing and settlement. A consuming module (a future storefront, or the existing checkout) resolves a snapshot once and stores it, so the order of record is stable and self-contained.

## Resolving a price

Reading `ProductPart.Price` directly couples a caller to today's flat, per-item pricing. To keep checkout, payment, and future ordering flows stable while pricing rules evolve, resolve prices through a seam instead:

- **`PriceResult`** is an immutable value that always pairs an amount with the currency it is expressed in — its `UnitPrice`, `Currency`, `Quantity`, and computed `Subtotal`. A price is never passed around without its currency.
- **`IPriceResolver`** resolves a `PriceResult` from a `ProductSnapshotContext`. The default resolver returns the product's list price tagged with the product-owned currency. When the context requests another currency, a price the product lists in it wins; otherwise a one-time price is converted through **`IShopperCurrency`**. The default `IShopperCurrency` (`ProductOwnShopperCurrency`) converts nothing, and a recurring price or one the buyer names is never converted, so the resolver returns `null` and logs a warning rather than charge a price in the wrong currency. [Markets](markets.md#the-shoppers-currency-seam) registers an `IShopperCurrency` that converts by the shopper's market.

A future pricing engine (customer-specific pricing, contract rates) can replace `IPriceResolver` to produce the same `PriceResult` without changing any consumer.

## Selling one product several ways

A product with a single amount forces a separate product for every way of selling the same thing. The
**Product prices** part removes that: attach it and the product carries as many prices as it needs, and the
buyer picks one.

Each price is charged **once** or **recurring**, and carries everything that decides what a gateway is told:

| | |
| --- | --- |
| Name and amount | What the buyer sees and pays, in the price's own currency. |
| Interval, cycles | How often it repeats and how many times before it ends on its own. |
| Trial, start delay | Free days, or a first cycle that begins later. |
| Setup fee | A one-time amount charged alongside the first cycle. |
| Buyer names the amount | Pay-what-you-want, bounded by a minimum and a maximum. |
| Quantity | Seats, bounded by a maximum. |
| Offered from / until | A window, so a launch price stops selling itself on its own. |

A **recurring price is what makes a product subscribable** — no separate flag. The same product can offer a
one-time purchase and a monthly plan side by side.

Every rule is enforced when the price is resolved, not only in the editor, because the choice arrives from
a form: an amount below the minimum, a quantity above the maximum, a price that is no longer offered, or a
currency that does not match are each refused rather than adjusted. A product with no prices of its own is
still sold at the single amount on its **Product** part, so the part is opt-in and nothing has to be
migrated to keep working.

:::note
A price keeps its identifier for its whole life. Editing a price changes what new buyers pay and never what
an existing agreement bills, and withdrawing one deactivates it rather than deleting it — an agreement
created from a price still names it.
:::

## Selling a product in variants

The **Product variants** part (`ProductVariantsPart`) sells one product in versions, such as sizes and colors.

- The **option names** belong to the content type, in the part's settings (`ProductVariantsPartSettings.Options`), each with a **Filterable** switch. Every product of the type uses the same options, so listings filter them the same way.
- Each product holds its **values** per option (`ProductVariantsPart.Options`) and one **variant** per combination of values (`ProductVariantsPart.Variants`). A `ProductVariant` has a stable `VariantId`, its option values, and optional `Sku`, `Price`, `CompareAtPrice` and `ImagePath` (one of the product's own images, from its first media field with images), plus `Enabled`. An empty SKU or price falls back to the product's.
- Saving the editor recomputes the combinations with `ProductVariants.Combine`: a combination that already has a variant keeps it (and its SKU and prices), new combinations get new variants, and combinations whose values were removed are dropped. When the type gains or loses an option, a variant that agrees with a combination on the options both have carries on as the first such combination; exact matches are settled first, so they are never taken. Duplicate variant SKUs are refused.
- **Pricing.** `IProductSnapshotResolver` refuses a variant product without a `VariantId`, or with a disabled or unknown one, so no checkout path can sell a product without a chosen variant. `IPriceResolver` prices a chosen variant at its own price, ahead of any **Product prices** list.
- **Carts and orders.** `ICartService.AddAsync` takes a `variantId`; a cart line (`CartLine.VariantId`) and an order line (`OrderLine.VariantId` and `VariantTitle`) record it, the line's title reads like *Cap (L / Gloss)*, and its SKU is the variant's. The *Add to cart* form offers the enabled variants as one list with their prices, which the `product-variant-picker` script (Carts module) replaces with one list per option, offering only values that make a variant on sale. On a product's page it shows the chosen variant's price, SKU and image; gallery images carry `data-media-path` so it can find the variant's photo.
- **Listings.** `ProductVariantsSearchDocumentContributor` adds to the product's search record the option values of the enabled variants under the `Option.{Name}` attribute key, their SKUs as searchable text, one price row per variant price (so a price filter finds the product when any variant is in range), and the lowest price with `MaxPrice` set when prices differ, which cards show as *From $25*. A filterable option appears in the listing editor like a field, with the default URL key `option-{name}`.

Changing a type's option names rebuilds the search index, like any other change to what products are filtered on.


## Merchandising

These work on the **Product** part and need nothing else unless a feature is named.

- **Scheduled sales.** `ProductPart.SalePrice`, `SaleStartsUtc` and `SaleEndsUtc`, and `ProductVariant.SalePrice`. `ProductSale.GetPrice` returns the price in force at a time, which the snapshot resolver, the search records and *Add to cart* use; during a sale the regular price becomes the compare-at price. The `ProductSaleIndex` and a background task refresh the search records of products whose sale starts or ends, within a minute.
- **Quantity rules and volume pricing.** `ProductPart.MinQuantity`, `MaxQuantity`, `QuantityStep`, `PriceTiers` (`ProductPriceTier`: `MinQuantity`, `PercentOff`) and `RoleDiscounts` (`ProductRoleDiscount`: `Role`, `PercentOff`). `ProductVolumePricing.GetPercentOff` takes the best single discount the quantity and the buyer's roles reach, and the default price resolver applies it, rounded to the cent. The cart refuses a quantity that breaks the rules (`ProductVolumePricing.Check`).
- **Add-ons.** `ProductPart.AddOns` (`ProductAddOn`: `AddOnId`, `Label`, `Kind` *Text* or *Checkbox*, `Price`, `PriceBasis` *PerUnit*, *PerPerson* or *PercentOfPrice*, `Required`). They're priced by the first cart line handler: per unit, per person a unit is for (`CartLineEvaluateContext.PeoplePerUnit`, set by handlers such as bookings), or as a percent of the unit's price before add-ons. `ProductAddOns` normalizes what a buyer chose (the product's own add-ons only, texts trimmed to 200 characters), finds a required one left out, and works out the surcharge per unit. `ICartService.AddAsync` has an overload taking the add-ons (a default interface method, so other implementations keep compiling); `CartLine.AddOns` keeps them, and two lines of a product differ when their add-ons do. `CartLinePricing.AddOns` describes them, `OrderLine.AddOns` (`OrderLineAddOn`) copies them, and `OrderLineText.Describe` reads a line with them for invoices, emails and exports. The *Add to cart* form posts them as `addOns[{AddOnId}]` on the product's own page.
- **External products.** `ProductPart.ExternalUrl` and `ExternalButtonText`. A product with one can't be bought (`CanBePurchased` is false); `ExternalProductActionDisplayDriver` puts the link in the product's `ProductAction` display in place of *Add to cart*.
- **Recommendations** (feature `CrestApps.OrchardCore.Products.Recommendations`). `ProductRecommendationsPart` keeps `UpsellIds` and `CrossSellIds` on the product. The product page gets `ProductUpsells`, `ProductRelated` (products sharing its deepest category terms, from `ProductRecommendationService`) and `ProductRecentlyViewed` shapes; the cart and the order confirmation render a `CartRecommendations` shape of the cross-sells (its `Heading` argument names it). Cards for these strips come from `ProductListingService.BuildCardsAsync` and, for scripts, `products/cards`.
- **Comparison** (feature `CrestApps.OrchardCore.Products.Comparison`). A `ProductCompareToggle` shape in the `ProductAction` display, a bar that keeps up to four product ids in the browser, and a `products/compare` page that renders their cards with their specifications.
- **Grouped products** (feature `CrestApps.OrchardCore.Products.Grouped`). `ProductGroupPart.ProductIds`, up to 20, edited with a picker. The Carts module's `GroupedProductsDisplayDriver` renders a `GroupedProducts` shape in the product's `ProductAction` display: a quantity per product on its page, posted to `cart/add-many` (`CartConstants.RouteNames.AddMany`), or a link to the page on a card. A grouped product saves without a price.
- **Bundles** (feature `CrestApps.OrchardCore.Products.Bundles`). `ProductBundlePart.Items` (`ProductBundleItem`: `ContentItemId`, `Title`, `Quantity`), up to 10, indexed one row per part in `ProductBundleIndex`. `ProductBundles.GetAvailability` works out what the parts can make; the Inventory module answers bundles with it before its stock items, and expands a bundle line into its parts when it holds, sells and restocks. `OrderLine.BundleItems` keeps the parts as they were when the order was placed, and `OrderLineText.DescribeForPacking` lists what to put in the box. The page gets a `ProductBundleContents` shape.
- **Import and export** (with [Content Transfer](content-transfer)). `ProductPartContentImportHandler` adds the `ProductPart_*` columns (price, compare-at and sale prices with the sale's dates, SKU, currency, external address and label, quantity rules), and `ProductVariantsPartContentImportHandler` the `ProductVariantsPart_Options` column (`Size: S, M; Color: Red, Blue`, kept to the options the type offers) and `ProductVariantsPart_Variants` column (a JSON list of each variant's options, SKU, prices and whether it's sold). `StoreExportReaderTransformer` reads a Shopify or WooCommerce product export (`ProductCsv.Read`) as one row per product with those columns, the category and brand as taxonomy term names, and stock; a product already in the store, matched by its SKU, a variant's SKU or its handle, gets its content item id so the import updates it. For any product import, `ProductImportRowFilter` (a Content Transfer row filter) looks up each batch's SKUs, the product's and its variants', in one query of `ProductSkuIndex`: a row without a content item id gets the id of the product with its SKU when the file has a `ContentItemId` column, and is skipped with why when it hasn't; a row whose SKU an earlier row of the file used is skipped too. The product search behind the merchant's product pickers (`Admin/api/crestapps/products/search`) moved to the **Product Listing** feature.

## Product listing

| | |
| --- | --- |
| **Feature Name** | Product Listing |
| **Feature ID** | `CrestApps.OrchardCore.Products.Listing` |
| **Category** | Content Management |

The **Product Listing** feature lists products on the front end. Visitors can filter, sort, search and page through them, switch between a grid and a list, and open a product page with its price and specifications. It works for products that are sold online and for products that are only listed.

### Choosing what visitors can filter on

Product attributes are ordinary content fields on the product type. The settings of a field on a product type's part gain a **Product listing** section:

| Setting | Effect |
| --- | --- |
| **Filterable** | Visitors can filter listings by the field. |
| **Sortable** | Visitors can sort listings by the field: numbers, dates and times by value, and text and taxonomy fields by the name visitors see. |
| **Searchable** | Keyword search matches the field's value. |
| **Show on product cards** | Shown as a short chip on the product's card. |
| **Show in specifications** | Shown in the specification table of list rows and the product page. |
| **Listing label** | The label visitors see. Defaults to the field's display name. |
| **Number format** and **Unit** | For number fields: a .NET format string, such as `0` for a year, and a unit, such as `mi`. |
| **Swatch colors** | For text and taxonomy fields: the color of each value when a filter draws them as color swatches, one per line, such as `Midnight = #191970` or `Sky = light blue`. |
| **Color field on terms** | For taxonomy fields: the name of a text field on the terms that holds each term's color, so a color is set once on its term. |

Text, multi-text, numeric, boolean, date, date and time, time, link, content picker, user picker and taxonomy fields are supported, and HTML and Markdown fields can be searched (their text without markup). Dates are indexed as `yyyyMMdd` numbers (`ProductDateNumbers`) so they filter by range, with ready-made ranges by year, and read as ISO dates in the URL, such as `?available_min=2026-01-15`. Times are minutes since midnight. A module supports another field type by implementing `IProductAttributeValueProvider` (in Products.Core, with `FieldValueReader` to read the field's stored JSON). A taxonomy field can offer one level of its tree as a filter, so a *Make and model* taxonomy gives a **Make** filter and a **Model** filter. Terms appear in URLs by a readable slug made from their name by Orchard Core's `ISlugService`, such as `?make=toyota&model=camry`, and a link that still uses a term's identifier keeps working.

The section appears only for fields whose part is attached to a type with the Product part, so the fields of articles, pages and other content are left alone. A part used by several types qualifies when any of them is a product type.

The price, the content type, whether a product can be bought online and whether it is featured are always available as attributes, and a type sold in variants adds its filterable options.

### Managing products and listings

The **Commerce** admin menu has two lists:

- **Products** lists every content item whose type carries the Product part, such as goods, listed items and subscription plans, with a filter by type. Its **New** button offers only product types.
- **Product Listings** lists the listing pages. It appears when the Product Listing feature is enabled.

Both are Orchard Core's content list narrowed to those types. The type names are kept in a per-tenant cache that is refreshed whenever a content type or its parts change.

### Adding a listing page

Create a **Product Listing** content item, from **Commerce → Product Listings** or **Content → New**. Its editor sets:

- the product types it lists: **Every product type**, which also lists types added later, or **Only these product types** with at least one ticked. The filters, the sorts and the default sort's options are grouped by type: what every product has (the price, the type, whether it can be bought online), then each type's own fields, then fields several types share. Only the groups of the listed types show, and saving drops the filters and sorts of types that are not listed, so a vehicle listing shows the vehicle's filters and not the property's;
- **Show filters**: turn it off to list the products without filters, for example a short list of plans. A listing without filters and without keyword search has no filters panel, and its results take the full width;
- the filters it offers, either one for every filterable field or a chosen set, each with a label, a URL key, a kind (*Checkboxes*, *Radio buttons*, *Range* or *Toggle*), a [style](#filter-styles), [ready-made ranges](#ranges) above a range filter, the number of options shown before *See more*, and whether it starts collapsed;
- *Shown after*, which hides a filter until another filter has a selection, for example **Model** after **Make**;
- **Let visitors change the sort**: turn it off to always show the products in the default sort's order. The default sort can then be any sort, even one not offered;
- the sorts, the default sort, the grid or list layout, the shape of the product images (landscape, square, portrait or wide), the page sizes, and keyword search;
- **Paging** (`ProductListingPart.Paging`): *Numbered pages*, *A Load more button* or *Infinite scroll*. Every mode renders real page links; with the other two the script hides them, shows a button that fetches the next page and appends its cards (infinite scroll presses it when it comes near the viewport), keeps the result count right, and replaces the address so a reload starts from the last page shown. A page opened directly, such as `?page=3`, keeps its page links so the visitor can go back to the first;
- **Search index** (`ProductListingPart.IndexProfileName`): the database by default, or a product search index; see [Search servers](#search-servers).

The editor keeps each filter row to its main settings, with the rest under **More settings** and summed up beside the field name. Rows reorder by dragging their handle (Orchard Core's `Sortable` resource) or with the arrow keys on it; each row still posts an `Order` number, and a move hands its group's numbers out again so rows of other groups keep their place. The editor script is the `product-listing-editor` resource.

The page is an ordinary content item, so it has its own URL, menu entry and permissions.

#### Filter styles

A filter's **kind** decides what it matches: several values, one value, a minimum and maximum, or *yes*. Its **style** decides how it is drawn. The editor offers only the styles that suit the kind:

| Style | Kinds | Draws |
| --- | --- | --- |
| **List** | All | Options with their counts. The default. |
| **Buttons** | Checkboxes, Radio buttons | A row of buttons, which suits short values such as sizes. |
| **Color swatches** | Checkboxes, Radio buttons | A row of colored circles with their names. |

A swatch's color comes, in order, from the field's **Swatch colors**, then the **Color field on terms**, then the option's own name when that is a hex color or a CSS color name, such as *Navy* or *Light Blue*. An option with none of these shows an empty circle.

Modules can add styles; see [Adding a filter style](#adding-a-filter-style).

#### Ranges

A range filter offers a minimum and a maximum box, within the lowest and highest values of the matching products. Tick **Ranges** to also offer ready-made ranges above the boxes:

- leave the box beside it empty for round ranges worked out from the products, such as *up to $50, $50 to $100, $100 to $150, $150 to $200, $200 and up*;
- or type your own, separated by commas: `-25` for up to 25, `25-75` for 25 to 75, and `75+` for 75 and up. Ends are included, so `1-2, 3-4, 5+` suits bedrooms. Write numbers without thousands separators, with a dot for decimals.

Range filters work on number fields and the price.

A listing lists every kind of product the same way: goods sold online, items that are only listed, and [subscription plans](subscriptions#listing-your-plans), whose types carry the Product part automatically. A product with a recurring price shows its billing period, such as *$29 / month*.

Filtering is done with an ordinary GET form, so a listing works without JavaScript, every state has a URL that can be bookmarked or shared, and the back button works. With JavaScript, filters apply as they change on wide screens and the results update in place. On small screens the filters open in a side panel.

```text
/vehicles?make=toyota&model=camry&year_min=2016&price_max=20000&sort=price-asc&view=list&page=2&page-size=24
```

Unknown keys are ignored, the number of values per filter is capped, and page sizes must be one of the configured sizes. `q`, `sort`, `view`, `page` and `page-size` belong to the listing itself, so a filter cannot use them as its URL key.

The listing's markup uses Bootstrap 5 classes and works with themes on Bootstrap 5.0 and later.

### Product pages

Turn on **Show product details on the product page** in the type's Product part settings to lay out each product's page:

- a breadcrumb above the title;
- the product's images beside its price, highlights and actions, with thumbnails when it has several images;
- the specification table after the description.

The images come from the type's first media field that has images. The actions show what modules offer to do with the product, such as the **Sign up** button of a subscription plan, or else *Available to buy online* or an inquiry button; the inquiry button's label and link are set on the type, and `{id}` and `{title}` in the link are replaced with the product's identifier and title. Fields shown in the specification table, and the media field shown as the gallery, are not shown a second time on the page.

### Breadcrumbs

Listing and product pages carry a breadcrumb built with Orchard Core's `<breadcrumb>` tag helper, so it renders through the `Breadcrumb` and `BreadcrumbItem` shapes and a theme restyles it with every other trail of the site.

- A listing shows *Home › Shop*, followed by the categories picked in its tree filters: with *Footwear › Boots* picked, the trail is *Home › Shop › Footwear › Boots*.
- A product shows *Home*, the listing that shows its type, and its place in that listing's category tree: *Home › Shop › Footwear › Boots › Chelsea Boot*.

The trails are named `ProductListing` and `Product`, which gives their shape alternates, such as `BreadcrumbItem-Product-Title`.

### Theming

Every part of a listing page and a product page is a shape, so a theme overrides any part of it in Razor or in Liquid, without touching the rest:

| Shape | Shows | Alternates |
| --- | --- | --- |
| `ProductListingPart` | The listing page, arranging the shapes below | The usual content part alternates |
| `ProductListingBreadcrumb` | The listing's breadcrumb, in the page header | `ProductListingBreadcrumb-{ContentType}` |
| `ProductListingFilters` | The filters sidebar, an off-canvas panel on small screens | `ProductListingFilters-{ContentType}` |
| `ProductListingSearch` | The keyword search box | `ProductListingSearch-{ContentType}` |
| `ProductFacet` | One filter | `ProductFacet-{Kind}` (`Checkbox`, `Radio`, `Range`, `Toggle`), `ProductFacet-{Style}` (`Buttons`, `Swatches`, or a style a module adds), `ProductFacet-{Key}` |
| `ProductListingToolbar` | The result count, sort, page size and layout switch | `ProductListingToolbar-{ContentType}` |
| `ProductListingActiveFilters` | The picked filters, each removable | `ProductListingActiveFilters-{ContentType}` |
| `ProductListingResults` | The products of the page | `ProductListingResults-Grid`, `ProductListingResults-List` |
| `ProductCard` | One product in the results | `ProductCard-{ContentType}`, `ProductCard-List`, `ProductCard-List-{ContentType}` |
| `ProductPrice` | A price, with the compare-at price | |
| `ProductListingPager` | The pager | `ProductListingPager-{ContentType}` |
| `ProductBreadcrumb` | A product's breadcrumb, in the page header | |
| `ProductSummary` | The images beside the price, highlights and actions | |
| `ProductGallery` | The images | `ProductGallery-{ContentType}` |
| `ProductActions` | What a visitor can do with the product | `ProductActions-{ContentType}` |
| `Content` (display type `ProductAction`) | The actions modules add, on the product's card and page | `Content-{ContentType}.ProductAction` |
| `ProductSpecifications` | The specification table | |

The listing shapes' model has a `Listing` property with everything the page shows, and the product page shapes' model has a `Product` property. The view models are registered for Liquid, so a template such as this one, saved as `ProductCard` in **Design → Templates**, replaces every product card:

```liquid
<article class="product-card card h-100">
  <div class="card-body">
    <h3 class="h6"><a href="{{ Model.Url }}" class="stretched-link">{{ Model.Title }}</a></h3>
    <p class="small text-muted">{{ Model.Chips | join: " · " }}</p>
    {{ Model.Price | shape_render }}
    {% if Model.DiscountPercent %}<span class="badge bg-danger">-{{ Model.DiscountPercent }}%</span>{% endif %}
  </div>
</article>
```

### Adding a filter style

A module adds a style by registering it in `ProductFacetStyleOptions`, with the filter kinds it can draw, and providing a `ProductFacet-{Style}` template. The listing editor then offers it for those kinds, and a filter drawn in it gets the `ProductFacet__{Style}` alternate:

```csharp
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<ProductFacetStyleOptions>(options =>
            options.AddStyle("Stars", style =>
            {
                style.DisplayName = S["Star rating"];
                style.Kinds.Add(ProductFacetKind.Checkbox);
                style.Kinds.Add(ProductFacetKind.Radio);
            }));
    }
}
```

The template, `ProductFacet-Stars.cshtml` or a Liquid template named `ProductFacet-Stars`, receives a `ProductFacetViewModel` with the filter's options, their counts, their URLs, whether each is selected, and each option's swatch `Color`. A style with no kinds can draw every kind. A filter whose style is no longer registered, for example after its module is disabled, is drawn as a list.

The kinds are fixed, because each one is a different way of searching. A new way of drawing a filter is a style.

### Adding actions from your module

A module adds an action to products, such as a subscribe form or the [Carts](carts-and-orders) feature's *Add to cart* button, through the product's `ProductAction` display: its part's display driver places a shape in the **Actions** zone of that display type, and the shape shows on the product's card and on its page.

```csharp
return Initialize<MyActionViewModel>("MyAction", model => { /* ... */ })
    .Location("ProductAction", "Actions");
```

A module can also add to a product's search record by implementing `IProductSearchDocumentContributor`, which Subscriptions uses to give a plan its billing period and its position, and Inventory to say whether it is in stock (`ProductAttributeKeys.InStock`, offered as the automatic *In stock* toggle filter). Turning a contributor's feature on or off changes the index fingerprint, so the records are rebuilt. To offer what a contributor writes as an automatic filter or sort, implement `IProductListingEntryProvider`: [Product Reviews](reviews) adds *Customer rating* this way, from `ProductAttributeKeys.Rating`. When something other than the product changes what its record holds, call `IProductSearchIndexRefresher.RefreshAsync(contentItemIds)` to rebuild those records now, or `RequestRebuildAsync()` to rebuild them all on the next pass.

Merchandising lives on the **Product** part of every product: **Featured** (`ProductPart.Featured`) and **Position** (`ProductPart.Position`). Every listing offers **Recommended**, which orders by position with products without one last, and **Featured first**, and a *Featured* toggle filter. Ties in any sort go to featured products, then the newest. A contributor can set `ProductSearchDocument.Position` for products without one; Subscriptions does this with a plan's **Sort** value.

The pages are styled by a small stylesheet whose colors and sizes come from custom properties, which fall back to Bootstrap's own: `--product-accent`, `--product-sale-color`, `--product-success-color`, `--product-border-color`, `--product-muted-color`, `--product-surface`, `--product-surface-alt` and `--product-radius`. Set them on `:root` or on `.product-listing` to restyle the pages without replacing any template.

### The search index

Each published product has one search record, with one row per attribute value, kept in the tenant database. No external search service is needed.

- Publishing, unpublishing, deleting or importing a product refreshes its record.
- Publishing a taxonomy refreshes the products filed under it, so a renamed term shows its new name and slug.
- The index is rebuilt in the background when a product type, a field's listing settings or a taxonomy's terms change. Records are replaced product by product, so a listing never shows empty during a rebuild.
- Search results are kept in memory for a few minutes per tenant and per set of filters, and dropped as soon as a record changes. On a web farm another node can show a result up to five minutes old.
- Each record carries the product's culture from its `LocalizationPart`. A listing shows the products of the request's UI culture and the products that are not localized, and a product's breadcrumb uses a listing in the product's own culture.

### Search servers

A listing can search an index held by Elasticsearch, Lucene or Azure AI Search instead of the database. Each is an optional module built on Orchard Core Indexing:

| Feature | Module | Depends on |
| --- | --- | --- |
| Product Search (Elasticsearch) | `CrestApps.OrchardCore.Products.Elasticsearch` | `OrchardCore.Elasticsearch` |
| Product Search (Lucene) | `CrestApps.OrchardCore.Products.Lucene` | `OrchardCore.Lucene` |
| Product Search (Azure AI Search) | `CrestApps.OrchardCore.Products.AzureAI` | `OrchardCore.AzureAI` |

Each registers a `ProductSearch` index type, so **Search > Indexes** offers *Products (Elasticsearch)* and the like. How it works:

- **One record, every index.** The database record is built once. `ProductSearchIndexer` raises `IProductSearchIndexHandler` after it writes, removes or rebuilds records; while Orchard Core Indexing is enabled the Product Listing feature writes the same records to every index profile of the `ProductSearch` type through the provider's keyed `IDocumentIndexManager`. A created, reset or rebuilt index is filled from the records. When product types or their attributes change, each profile is saved again (so a provider that declares its fields up front declares the new ones), rebuilt and filled.
- **Shared fields.** `ProductSearchIndexFields` writes a record as the same fields for every provider: key, type, culture (`neutral` when not localized), title, a lower-case title for sorting, SKU, keywords, price, featured, position, published date and a sequence number. Each attribute gets fields under a safe name such as `a_vehicle_year_1a2b3c4d`: its values (`_v`) for filters, facet tokens (`_f`) that carry the level, label and order next to the value, its numbers (`_n`), its smallest and largest number (`_sn`, `_nx`) and a sort text (`_st`). Values longer than 256 characters are searched but not filtered.
- **Shared query plan.** `ProductSearchIndexQuery` turns a listing query into filters on those fields and the same sort as the database. A facet counts with every filter but its own (`FiltersExcept`), so each option says how many products it adds.
- **Providers.** Each module adds a keyed `IProductIndexSearchService`. Elasticsearch answers in one request with a post filter and one filter aggregation per facet. Lucene has no facets in Orchard Core, so counts come from a collector over `FieldCache` term ordinals. Azure AI Search declares every field from the attribute catalog; because Orchard Core cannot declare a list of numbers there, its ranges and bounds read each attribute's smallest and largest number, which is exact for single-number attributes such as a year or a price, and a `_has` companion keeps products without a value last.
- **Fallback.** `ProductSearchRouter` searches the index a listing names, and the database when it names none, when the index or its provider is gone, or when the server fails (logged as a warning).

Keyword search behaves a little differently on a search server: it matches the start of words and ranks the best matches first, where the database matches any part of a word. A module can add another search server the same way, referencing only `CrestApps.OrchardCore.Products.Core`: register a `ProductSearch` indexing source, write its mapping in a `ProductSearchIndexProfileHandlerBase` created with the provider's name (it configures only that provider's product search indexes), read the attributes from `ProductAttributeCatalog`, and register an `IProductIndexSearchService` under the provider's name.

### Search engines

Search engine tags come from Orchard Core's [SEO](https://docs.orchardcore.net/en/latest/reference/modules/Seo/) module. Enable it and attach the **SEO** part to the **Product Listing** type and to your product types:

- The listing's SEO part sets its title, description and social tags, and its canonical URL points every filtered, sorted or paged state of the listing at the listing's own URL.
- When the address carries a filter or a keyword, the listing asks for `noindex, follow`, so crawlers follow its links to the products without indexing every combination of filters. The page number, sort, page size and layout do not count as filters.
- Each product's SEO part sets its own description and social tags.

### Samples

Three recipes show the feature end to end. They need a front-end theme on Bootstrap 5, such as TheTheme; a site set up with the Blank recipe has none, so set one first.

- **Product listing: online store sample** creates a *Store Product* type with category and brand taxonomies, color, size, material and rating attributes, 30 products that can all be bought online, and a **Shop** listing page at `/shop`. It enables [Carts](carts-and-orders), so every product has *Add to cart*, plus Orders and [Customers](customers), and adds two sample customers: a person with a home and a work address, and a business in Canada with a tax id. It also enables [Inventory](inventory) and [Shipping](shipping): every product is counted (25 of most, a few at 2 to 4 so they show *Only N left*, and the *Adventure Works Everyday Backpack* sold out) and has a shipping weight, with boots in a *Bulky* class; the `Stock`, `ShippingZones` and `Settings` steps add the counts, zones for the United States (standard, express by weight, free from $100), Canada and the rest of the world, and three carriers with tracking links. The category filter offers the top level of the category tree, and a *Type* filter offers the level below once a category is picked.
- **Product listing: real estate sample** creates a *Property* type that is listed but never sold online, with a city and neighborhood taxonomy, type, bedrooms, bathrooms, living area, year built, features and status, 20 properties (some with the price on request and an inquiry button that schedules a viewing), and a **Homes for sale** listing page at `/homes`.
- **Product listing: vehicle dealership sample** creates a *Vehicle* product type with make and model, body style, year, mileage, fuel, drivetrain and color attributes, 28 vehicles (some for sale online, the rest listed only), and a **Vehicles** listing page at `/vehicles`.

## Product feeds

The **Product Feeds** feature (`CrestApps.OrchardCore.Products.Feeds`) publishes the products for shopping channels:

- `feeds/products.xml` is Google Merchant Center's RSS 2.0 with the `g:` attributes, which Meta and TikTok catalogs read too.
- `feeds/products.csv` has the same columns.

Both take `?currency=` (a market's currency, converted with `IShopperCurrency`) and `?key=` when `ProductFeedSettings.Key` is set. `ProductFeedService` caches a feed for 15 minutes per currency.

`ProductFeedBuilder` reads the published items of every product type, 200 at a time. An item is a product that can be bought here with its price shown and isn't recurring, or each enabled variant of one with variants. Its attributes are:

| Attribute | From |
| --- | --- |
| `id`, `item_group_id` | The SKU, or the item id; a variant's group is its product. |
| `title`, `description` | The title (with the variant's options), and `HtmlBodyPart`, `MarkdownBodyPart` or `SeoMetaPart` as plain text. |
| `link`, `image_link`, `additional_image_link` | The display route on the site's `BaseUrl`, and the first `MediaField`'s photos (a variant's own first). |
| `availability`, `availability_date` | `IProductAvailabilityProvider`: `in_stock` (or untracked), `out_of_stock`, `backorder`, or `preorder` with its release date. |
| `price`, `sale_price`, `sale_price_effective_date` | The variant's, the product's or its default one-time price, never a buyer's own (`IBuyerPriceProvider` isn't asked); the sale price while a sale is on or coming, with its dates. |
| `brand`, `gtin`, `mpn`, `identifier_exists` | `ProductFeedPart`, else a term of a field or taxonomy named for brands, else `DefaultBrand`; `identifier_exists` is `no` without a brand and a GTIN or MPN. |
| `condition`, `google_product_category`, `product_type` | `ProductFeedPart` or the defaults; the path of the product's first other taxonomy term. |
| `color`, `size` | A variant's options named Color or Size, or a text field named Color. |

`ProductFeedValidator` checks each item against Merchant Center's rules: the lengths, absolute links, a photo, an availability and condition it knows, a price above zero, a sale price below it, and a GTIN's check digit. An item that fails is left out and listed. **Settings > Commerce > Product feeds** (`ManageProductFeeds`) shows the feed addresses, the items in and why the others aren't, and the defaults.

The attachable **Product Feed** part (`ProductFeedPart`: `Brand`, `Gtin`, `VariantGtins`, `Mpn`, `Condition`, `GoogleProductCategory`, `ExcludeFromFeeds`) goes on the product types that need them.

## API

The **Catalog API** feature (`CrestApps.OrchardCore.Products.Api`, it needs Product Listing) maps `GET products` and `GET products/{id}` into the commerce JSON API at `api/commerce/v1`. Both allow guests. The list is searched through `IProductSearchService` over the product types the caller may view (`ViewContent`), with the listing pages' sort keys (`newest`, `price-asc`, `price-desc`, `name`, `featured`, or the recommended order). Each product is priced for the caller through `IPriceResolver`, as the product page prices it, and its availability comes from the `IProductAvailabilityProvider`s. With the **Inventory API** on, `GET products/{id}/availability` gives the stock at each location.

See [Commerce API](commerce-api.md) for the endpoints, the conventions they share, and how apps sign in, and [Headless commerce API](../user-manual/commerce/headless-api.md) in the User Manual for setting it up in the admin.

## Report Builder data source

When **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) is enabled, which the Report Builder enables, the module registers the **Products** data source (technical name `Products`). The Report Builder and Orchard Core data pipelines both read it.

The source uses the content permissions, as the content screens do. A principal reads the products of each product type it may view (**View content** for that type). For a type it may also edit (**Edit content**), it reads the latest version of each product, drafts included; for any other type, the published version. A product gives its rows once, whatever number of versions it has. Without a principal, or when no product type is readable, the source lists no data set. Because the readable products depend on the principal, the data sets are never grouped in the database; the report groups the rows it reads.

The rows come newest first. The content item index has no date that is never empty, so no date range is applied while the rows are read: the report filters the dates itself. A join on the product's identifier, and a filter on the content type, narrow the read.

| Data set | Registered by | Rows |
| --- | --- | --- |
| `Products` | Products | One per product. |
| `ProductVariants` | Products | One per variant of a product sold in variants. |
| `ProductPrices` | Products | One per named price a product is offered at. |
| `ProductBundleItems` | Product Bundles | One per product a bundle is made of. |
| `ProductAttributes` | Product Listing | One per attribute value of a listed product. |

Other data sets point at these ones by name: an order line's product and variant point at `Products/Products/ContentItemId` and `Products/ProductVariants/VariantId`.

### Products

| Field | Label | Notes |
| --- | --- | --- |
| `ContentItemId` | Product ID | Identifier. A join on it narrows the read. |
| `ContentItemVersionId` | Version ID | The version read. |
| `ContentType` | Product type | The content type. A filter on it narrows the read. |
| `Title` | Product | The display text. |
| `Sku` | SKU | |
| `ProductType` | Kind of product | `Good`, `Service`, `Digital` or `Undefined`, from the content type's settings. |
| `IsPurchasable` | For sale | Whether the product can be bought online, from the item and its type's sales mode. |
| `Featured` | Featured | |
| `IsBundle` | Bundle | Whether the product is made of other products. |
| `VariantCount` | Variants | |
| `Categories` | Category IDs | The taxonomy terms the product's taxonomy fields hold, separated by commas. |
| `TaxCategoryCode` | Tax category | From the Taxation part. |
| `Published` | Published | Whether this is the published version. |
| `Owner` | Owner ID | Points at the users data set. |
| `Currency` | Currency | The product's currency, else its content type's default, in upper case. |
| `Price` | Price | In the product's currency. Empty for a product sold only at its named prices, or whose price is not shown. |
| `CompareAtPrice` | Compare-at price | In the product's currency. |
| `SalePrice` | Sale price | In the product's currency. |
| `SaleStartsUtc`, `SaleEndsUtc` | Sale starts, Sale ends | |
| `PriceCount` | Named prices | |
| `CreatedUtc`, `ModifiedUtc`, `PublishedUtc` | Created, Last changed, Published on | |

### ProductVariants

| Field | Label | Notes |
| --- | --- | --- |
| `ProductId` | Product ID | Points at `Products/Products/ContentItemId`. A join on it narrows the read. |
| `ProductTitle`, `ContentType`, `Published` | Product, Product type, Published | From the product. |
| `Currency` | Currency | The product's currency. |
| `VariantId` | Variant ID | Identifier. |
| `Position` | Variant number | |
| `VariantTitle` | Variant | The option values in option order, such as Red / M. |
| `Sku` | SKU | The variant's own SKU, else the product's. |
| `Price` | Price | The variant's own price, else the product's, in the product's currency. |
| `HasOwnPrice` | Own price | |
| `CompareAtPrice` | Compare-at price | The variant's own, else the product's, in the product's currency. |
| `SalePrice` | Sale price | In the product's currency. A variant priced on its own has only its own sale price. |
| `Enabled` | Can be chosen | |

The variant's image is not exposed.

### ProductPrices

| Field | Label | Notes |
| --- | --- | --- |
| `ProductId` | Product ID | Points at `Products/Products/ContentItemId`. A join on it narrows the read. |
| `ProductTitle`, `ContentType`, `Published` | Product, Product type, Published | From the product. |
| `PriceId` | Price ID | Identifier. |
| `Position` | Price number | |
| `PriceName` | Price | The name shown to the buyer, such as Monthly. |
| `Kind` | Charged | `OneTime` or `Recurring`. |
| `Currency` | Currency | The price's currency. |
| `Amount` | Amount | In the price's currency. |
| `BillingDuration`, `Interval` | Billing every, Billing unit | The cycle of a recurring price. |
| `BillingCycleLimit` | Cycles | Empty when the price bills until it is canceled. |
| `TrialDays` | Trial days | |
| `SetupFee` | Setup fee | In the price's currency. |
| `AllowCustomAmount`, `MinimumAmount`, `MaximumAmount` | Buyer names the amount, Least amount, Most amount | The amounts are in the price's currency. |
| `IsDefault`, `IsActive` | Default, Active | |
| `EffectiveFromUtc`, `EffectiveToUtc` | Offered from, Offered until | |

### ProductBundleItems

| Field | Label | Notes |
| --- | --- | --- |
| `BundleId` | Bundle ID | The bundle. Points at `Products/Products/ContentItemId`. A join on it narrows the read. |
| `BundleTitle`, `ContentType`, `Published` | Bundle, Bundle type, Published | From the bundle. |
| `ProductId` | Product ID | The product in the bundle. Points at `Products/Products/ContentItemId`. |
| `ProductTitle` | Product | The product's title when the bundle was saved. |
| `Quantity` | Quantity | How many of the product each bundle holds. |
| `Position` | Item number | |

### ProductAttributes

The attribute values come from the search record the Product Listing feature keeps for each published product, in each culture. They are the values the listing filters and counts products by.

| Field | Label | Notes |
| --- | --- | --- |
| `ProductId` | Product ID | Points at `Products/Products/ContentItemId`. A join on it narrows the read. |
| `ProductTitle`, `ContentType` | Product, Product type | A filter on the content type narrows the read. |
| `Culture` | Culture | Empty for a product that shows in every culture. |
| `AttributeKey` | Attribute | The field the value comes from, or a name the listing keeps for its own values, such as `Product.Price`. |
| `Value` | Value | The text filters match on: the text itself, a term or content item identifier, or true or false. |
| `Label` | Label | The text shown to visitors. |
| `NumberValue`, `DateValue` | Number, Date | For numeric and date attributes. |
| `Level` | Level | The depth of a taxonomy term, or 0. |

## Recipes and schema

The **Product** part is defined and imported through Orchard Core's built-in `ContentDefinition` recipe step, and product content items through the built-in `Content` step.

When the **`CrestApps.OrchardCore.Recipes`** feature is enabled, JSON Schema is contributed for the `ProductPart` (its `Price`, `Currency`, and `Sku` payload and the `ProductPartSettings.Type` and `DefaultCurrency` options), giving editor validation and IntelliSense while authoring content-definition recipes.

When the same feature is enabled, JSON Schema is also contributed for the managed **`Currencies`** recipe step.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Products
```

Then enable **Products** in the **Orchard Core Admin Dashboard** under **Tools → Features**. Enable **Product Listing** too to list products on the front end. To search a search server, add and enable `CrestApps.OrchardCore.Products.Elasticsearch`, `CrestApps.OrchardCore.Products.Lucene` or `CrestApps.OrchardCore.Products.AzureAI`.

## Related modules

- [Subscriptions](subscriptions) — builds recurring billing flows on top of product-enabled content types.
- [Payments](payments) — the provider-agnostic payment framework used to charge for products.
- [Taxation](taxation) — determines tax for products that opt in via the Taxation part.
