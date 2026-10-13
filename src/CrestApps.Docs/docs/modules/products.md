---
sidebar_label: Products
sidebar_position: 12
title: Products
description: Attach product pricing and type metadata to any Orchard Core content type.
---

| | |
| --- | --- |
| **Feature Name** | Products |
| **Feature ID** | `CrestApps.OrchardCore.Products` |
| **Category** | Content Management |

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
| `Currency` | `string` | The ISO-4217 currency code the price is sold in (for example `USD`). When empty, the content type's `ProductPartSettings.DefaultCurrency` applies. A product owns its currency; prices are never converted between currencies. |
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

You can attach the part from the admin UI (**Content Definition → Content Types → *your type* → Add Parts → Product**) or in a migration:

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
- **`IProductSnapshotResolver`** resolves an `ISellableProduct` from a `ProductSnapshotContext` (the content item plus the requested currency, quantity, SKU, and variant). The default resolver reads the **Product** and **Taxation** parts and the `ProductPartSettings`.

Both the editable `ProductPart.Price` and the snapshot's unit price are `decimal`, the authoritative representation for stored financial records, so a price never suffers binary floating-point drift between editing and settlement. A consuming module (a future storefront, or the existing checkout) resolves a snapshot once and stores it, so the order of record is stable and self-contained.

## Resolving a price

Reading `ProductPart.Price` directly couples a caller to today's flat, per-item pricing. To keep checkout, payment, and future ordering flows stable while pricing rules evolve, resolve prices through a seam instead:

- **`PriceResult`** is an immutable value that always pairs an amount with the currency it is expressed in — its `UnitPrice`, `Currency`, `Quantity`, and computed `Subtotal`. A price is never passed around without its currency.
- **`IPriceResolver`** resolves a `PriceResult` from a `ProductSnapshotContext`. The default resolver returns the product's list price tagged with the product-owned currency. It never converts between currencies: when the context requests a currency that differs from the product's currency it returns `null` and logs a warning, so a price is never charged in the wrong currency.

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
| **Sortable** | Visitors can sort listings by the field. Offered for numbers, dates and single values. |
| **Searchable** | Keyword search matches the field's value. |
| **Show on product cards** | Shown as a short chip on the product's card. |
| **Show in specifications** | Shown in the specification table of list rows and the product page. |
| **Listing label** | The label visitors see. Defaults to the field's display name. |
| **Number format** and **Unit** | For number fields: a .NET format string, such as `0` for a year, and a unit, such as `mi`. |
| **Swatch colors** | For text and taxonomy fields: the color of each value when a filter draws them as color swatches, one per line, such as `Midnight = #191970` or `Sky = light blue`. |
| **Color field on terms** | For taxonomy fields: the name of a text field on the terms that holds each term's color, so a color is set once on its term. |

Text, multi-text, numeric, boolean, date, content picker and taxonomy fields are supported. A taxonomy field can offer one level of its tree as a filter, so a *Make and model* taxonomy gives a **Make** filter and a **Model** filter. Terms appear in URLs by a readable slug made from their name, such as `?make=toyota&model=camry`, and a link that still uses a term's identifier keeps working.

The section appears only for fields whose part is attached to a type with the Product part, so the fields of articles, pages and other content are left alone. A part used by several types qualifies when any of them is a product type.

The price, the content type and whether a product can be bought online are always available as attributes.

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
- the sorts, the default sort, the grid or list layout, the shape of the product images (landscape, square, portrait or wide), the page sizes, and keyword search.

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

A module can also add to a product's search record by implementing `IProductSearchDocumentContributor`, which Subscriptions uses to give a plan its billing period and its position.

A product type can have a merchant-chosen order. A module that stores a position adds it to the search record as the `Product.Position` attribute and implements `IProductPositionProvider` to name the types that have one. A listing of such a type then offers a **Recommended** sort, which orders by the position and puts products without one last. Subscriptions does this with the plan's **Sort** value.

The pages are styled by a small stylesheet whose colors and sizes come from custom properties, which fall back to Bootstrap's own: `--product-accent`, `--product-sale-color`, `--product-success-color`, `--product-border-color`, `--product-muted-color`, `--product-surface`, `--product-surface-alt` and `--product-radius`. Set them on `:root` or on `.product-listing` to restyle the pages without replacing any template.

### The search index

Each published product has one search record, with one row per attribute value, kept in the tenant database. No external search service is needed.

- Publishing, unpublishing, deleting or importing a product refreshes its record.
- Publishing a taxonomy refreshes the products filed under it, so a renamed term shows its new name and slug.
- The index is rebuilt in the background when a product type, a field's listing settings or a taxonomy's terms change. Records are replaced product by product, so a listing never shows empty during a rebuild.
- Search results are kept in memory for a few minutes per tenant and per set of filters, and dropped as soon as a record changes. On a web farm another node can show a result up to five minutes old.

### Search engines

Search engine tags come from Orchard Core's [SEO](https://docs.orchardcore.net/en/latest/reference/modules/Seo/) module. Enable it and attach the **SEO** part to the **Product Listing** type and to your product types:

- The listing's SEO part sets its title, description and social tags, and its canonical URL points every filtered, sorted or paged state of the listing at the listing's own URL.
- When the address carries a filter or a keyword, the listing asks for `noindex, follow`, so crawlers follow its links to the products without indexing every combination of filters. The page number, sort, page size and layout do not count as filters.
- Each product's SEO part sets its own description and social tags.

### Samples

Three recipes show the feature end to end:

- **Product listing: online store sample** creates a *Store Product* type with category and brand taxonomies, color, size, material and rating attributes, 30 products that can all be bought online, and a **Shop** listing page at `/shop`. It enables [Carts](carts-and-orders), so every product has *Add to cart*. The category filter offers the top level of the category tree, and a *Type* filter offers the level below once a category is picked.
- **Product listing: real estate sample** creates a *Property* type that is listed but never sold online, with a city and neighborhood taxonomy, type, bedrooms, bathrooms, living area, year built, features and status, 20 properties (some with the price on request and an inquiry button that schedules a viewing), and a **Homes for sale** listing page at `/homes`.
- **Product listing: vehicle dealership sample** creates a *Vehicle* product type with make and model, body style, year, mileage, fuel, drivetrain and color attributes, 28 vehicles (some for sale online, the rest listed only), and a **Vehicles** listing page at `/vehicles`.

## Recipes and schema

The **Product** part is defined and imported through Orchard Core's built-in `ContentDefinition` recipe step, and product content items through the built-in `Content` step.

When the **`CrestApps.OrchardCore.Recipes`** feature is enabled, JSON Schema is contributed for the `ProductPart` (its `Price`, `Currency`, and `Sku` payload and the `ProductPartSettings.Type` and `DefaultCurrency` options), giving editor validation and IntelliSense while authoring content-definition recipes.

When the same feature is enabled, JSON Schema is also contributed for the managed **`Currencies`** recipe step.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Products
```

Then enable **Products** in the **Orchard Core Admin Dashboard** under **Tools → Features**. Enable **Product Listing** too to list products on the front end.

## Related modules

- [Subscriptions](subscriptions) — builds recurring billing flows on top of product-enabled content types.
- [Payments](payments) — the provider-agnostic payment framework used to charge for products.
- [Taxation](taxation) — determines tax for products that opt in via the Taxation part.
