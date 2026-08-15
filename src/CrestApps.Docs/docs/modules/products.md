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

The **Products** module turns any Orchard Core content type into a purchasable product by attaching a reusable **`ProductPart`**. It provides the shared pricing and product-type metadata that other modules — most notably [Subscriptions](subscriptions) — build on top of, without prescribing how a product is sold.

## Overview

Once enabled, the module registers an attachable content part named **Product** (`ProductPart`). Attach it to a content type and every content item of that type gains a **Price** value, plus a design-time **Type** setting that classifies the product as a *Good*, *Service*, or *Plan*.

Because the part is a normal Orchard Core content part, products participate in the full CMS pipeline: they can be listed, queried, localized, versioned, indexed for search, and secured with the same permissions and workflows as any other content item.

## The Product part

`ProductPart` exposes a single content-item value:

| Property | Type | Description |
| --- | --- | --- |
| `Price` | `double` | The price of the item, expressed in the site's configured currency. |

The part's behavior is configured per content type through **`ProductPartSettings`**:

| Setting | Type | Description |
| --- | --- | --- |
| `Type` | `ProductType` | Classifies the product. One of `Undefined`, `Good`, `Service`, or `Planet`. |

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

## Taxation

Products participate in taxation through the [Taxation](taxation) framework — the Products module never calculates tax itself. When both **Products** and **Taxation** are enabled, add the **Taxation** part to a product content type and mark it taxable. A `ProductTaxableItemProvider` then exposes each product to the taxation engine as an `ITaxableItem`, supplying:

- the price from the **Product** part,
- the taxable-item kind derived from the product type,
- the tax category, classification, and external tax code from the **Taxation** part.

Because taxation is an optional feature dependency, products keep working normally when Taxation is disabled. See the [Taxation](taxation) module for how tax is then determined, snapshotted, and refunded.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Products
```

Then enable **Products** in the **Orchard Core Admin Dashboard** under **Tools → Features**.

## Related modules

- [Subscriptions](subscriptions) — builds recurring billing flows on top of product-enabled content types.
- [Payments](payments) — the provider-agnostic payment framework used to charge for products.
- [Taxation](taxation) — determines tax for products that opt in via the Taxation part.
