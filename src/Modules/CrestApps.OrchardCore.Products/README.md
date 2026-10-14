# CrestApps OrchardCore Products

The Products module turns any Orchard Core content type into something that can be sold, by attaching a
reusable **Product** part carrying a price, a currency, and a product type.

## Why this module exists

Several modules need to know what something costs: a subscription plan, an outstanding balance, a future
storefront. If each defined its own price field, the same product would carry three prices that could
disagree, and a report would have to know all three to add anything up.

The part deliberately says nothing about *how* the product is sold. That is the point: a price is a fact
about the item, while subscribing to it, adding it to a cart, or invoicing it are decisions made elsewhere.
Keeping those apart is what lets the same product be sold more than one way.

Because it is an ordinary content part, a product gets the whole CMS pipeline for free: listing, querying,
localization, versioning, search indexing, permissions, and workflows.

## Features

- **Products** (`CrestApps.OrchardCore.Products`) — the `ProductPart`, its editors and settings, and the
  shared **Commerce → Currencies** catalog.

## Currencies are a managed list

Editors pick from a managed catalog rather than typing a currency code. Free-form codes drift — `usd`,
`USD`, `US$` — and money that is grouped by a string will silently split into several currencies in a
report. The same catalog backs product defaults and subscription checkout settings so all three stay
aligned.

## The Product part

| Setting | Purpose |
| --- | --- |
| **Price** | What the item costs, in the currency below. |
| **Currency** | The ISO-4217 currency the price is expressed in. |
| **Type** (design time) | Classifies the item as a Good, a Service, or Digital, which downstream modules use for tax treatment and gateway metadata. |
| **Default currency** (design time) | Used when an item does not set its own. |

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/products>

## License

This project is licensed under the MIT License.
