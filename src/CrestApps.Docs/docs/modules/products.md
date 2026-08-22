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
| `Price` | `decimal` | The price of the item, expressed in the product's own `Currency`. |
| `Currency` | `string` | The ISO-4217 currency code the price is sold in (for example `USD`). When empty, the content type's `ProductPartSettings.DefaultCurrency` applies. A product owns its currency; prices are never converted between currencies. |
| `Sku` | `string` | An optional stock-keeping unit that uniquely identifies the product for carts, orders, and fulfilment. |

The part's behavior is configured per content type through **`ProductPartSettings`**:

| Setting | Type | Description |
| --- | --- | --- |
| `Type` | `ProductType` | Classifies the product. One of `Undefined`, `Good`, `Service`, or `Digital`. |
| `DefaultCurrency` | `string` | The ISO-4217 currency code applied to products of this type when an item does not set its own `Currency`. |

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

A future pricing engine (price schedules, quantity breaks, or customer-specific pricing) can replace `IPriceResolver` to produce the same `PriceResult` without changing any consumer.

## Recipes and schema

The **Product** part is defined and imported through Orchard Core's built-in `ContentDefinition` recipe step, and product content items through the built-in `Content` step.

When the **`CrestApps.OrchardCore.Recipes`** feature is enabled, JSON Schema is contributed for the `ProductPart` (its `Price`, `Currency`, and `Sku` payload and the `ProductPartSettings.Type` and `DefaultCurrency` options), giving editor validation and IntelliSense while authoring content-definition recipes.

When the same feature is enabled, JSON Schema is also contributed for the managed **`Currencies`** recipe step.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Products
```

Then enable **Products** in the **Orchard Core Admin Dashboard** under **Tools → Features**.

## Related modules

- [Subscriptions](subscriptions) — builds recurring billing flows on top of product-enabled content types.
- [Payments](payments) — the provider-agnostic payment framework used to charge for products.
- [Taxation](taxation) — determines tax for products that opt in via the Taxation part.
