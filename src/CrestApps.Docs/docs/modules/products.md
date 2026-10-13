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

Product attributes are ordinary content fields on the product type. Each field's settings gain a **Product listing** section:

| Setting | Effect |
| --- | --- |
| **Filterable** | Visitors can filter listings by the field. |
| **Sortable** | Visitors can sort listings by the field. Offered for numbers, dates and single values. |
| **Searchable** | Keyword search matches the field's value. |
| **Show on product cards** | Shown as a short chip on the product's card. |
| **Show in specifications** | Shown in the specification table of list rows and the product page. |
| **Listing label** | The label visitors see. Defaults to the field's display name. |
| **Number format** and **Unit** | For number fields: a .NET format string, such as `0` for a year, and a unit, such as `mi`. |

Text, multi-text, numeric, boolean, date, content picker and taxonomy fields are supported. A taxonomy field can offer one level of its tree as a filter, so a *Make and model* taxonomy gives a **Make** filter and a **Model** filter.

The price, the content type and whether a product can be bought online are always available as attributes.

### Adding a listing page

Create a **Product Listing** content item. Its editor sets:

- the product types it lists (every product type when none is selected);
- the filters it offers, either one for every filterable field or a chosen set, each with a label, a URL key, a kind (*Checkboxes*, *Radio buttons*, *Range* or *Toggle*), the number of options shown before *See more*, and whether it starts collapsed;
- *Shown after*, which hides a filter until another filter has a selection, for example **Model** after **Make**;
- the sorts, the default sort, the grid or list layout, the page sizes, and keyword search.

The page is an ordinary content item, so it has its own URL, menu entry and permissions.

Filtering is done with an ordinary GET form, so a listing works without JavaScript, every state has a URL that can be bookmarked or shared, and the back button works. With JavaScript, filters apply as they change on wide screens and the results update in place. On small screens the filters open in a side panel.

```text
/vehicles?make=<term-id>&year_min=2016&price_max=20000&sort=price-asc&view=list&page=2
```

Unknown keys are ignored, the number of values per filter is capped, and page sizes must be one of the configured sizes.

The listing's markup uses Bootstrap 5 classes and works with themes on Bootstrap 5.0 and later.

### Product pages

Turn on **Show product details on the product page** in the type's Product part settings to add a details block to each product's page: the price, the specification table, and either *Available to buy online* or an inquiry button. The inquiry button's label and link are set on the type; `{id}` and `{title}` in the link are replaced with the product's identifier and title. Fields shown in the specification table are not shown a second time on the page.

### The search index

Each published product has one search record, with one row per attribute value, kept in the tenant database. Publishing, unpublishing, deleting or importing a product refreshes its record, and the whole index is rebuilt in the background when a product type or a field's listing settings change. No external search service is needed.

### Sample

The **Product listing: vehicle dealership sample** recipe creates a *Vehicle* product type with make and model, body style, year, mileage, fuel, drivetrain and color attributes, 28 vehicles (some for sale online, the rest listed only), and a **Vehicles** listing page at `/vehicles`.

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
