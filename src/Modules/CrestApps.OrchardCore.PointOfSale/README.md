# Point of Sale

Sells at your stores' counters over the commerce API, from a till app or the web register: registers at each location, shifts with their cash, sales of products and of amounts by department, paid in cash, by card, gift card or a mix, receipts, and returns at the till. Each sale is an order of its store, so its stock comes off that store and reports show sales by store and channel.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Point of Sale | `CrestApps.OrchardCore.PointOfSale` | Sells at your stores' counters over the commerce API, from a till app or the web register: registers at each location, shifts with their cash, sales of products and of amounts by department, paid in cash, by card, gift card or a mix, receipts, and returns at the till. Each sale is an order of its store, so its stock comes off that store and reports show sales by store and channel. |
| Point of Sale Web Register | `CrestApps.OrchardCore.PointOfSale.WebRegister` | Rings up sales in the browser at your stores' registers, with no till app: department keys for amounts sold without an item to track, products by SKU or barcode scanner, cash and card on the counter terminal, receipts, and opening, counting and closing the shift. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Orders`](../CrestApps.OrchardCore.Orders/README.md)
- [`CrestApps.OrchardCore.Inventory`](../CrestApps.OrchardCore.Inventory/README.md)
- `CrestApps.OrchardCore.Checkout.Api`

## Documentation

See [Point of Sale](../../CrestApps.Docs/docs/modules/point-of-sale.md) in the technical manual.
