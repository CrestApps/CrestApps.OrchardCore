# Inventory

Counts how many units of each product and variant are on hand, with a history of every change, under Commerce > Inventory.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Inventory | `CrestApps.OrchardCore.Inventory` | Counts how many units of each product and variant are on hand, with a history of every change, under Commerce > Inventory. |
| Back-in-Stock Alerts | `CrestApps.OrchardCore.Inventory.StockAlerts` | Lets shoppers ask to be emailed when a product that's sold out is back in stock, and emails them, once, when it is. |
| Purchase Orders | `CrestApps.OrchardCore.Inventory.PurchaseOrders` | Orders stock from suppliers and receives it at a location, under Commerce > Purchase orders. |
| Inventory API | `CrestApps.OrchardCore.Inventory.Api` | Gives stock at each location over the commerce JSON API, and lets staff receive, adjust and move it. Enable OpenAPI to document it. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Products`](../CrestApps.OrchardCore.Products/README.md)
- `OrchardCore.Contents`
- `OrchardCore.Email`
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)

## Extension points

- `IInventoryLocationService` (CrestApps.OrchardCore.Inventory.Core): The places stock is kept, in the order orders take units from them.
- `IInventoryService` (CrestApps.OrchardCore.Inventory.Core): Changes stock.
- `IStockLevelHandler` (CrestApps.OrchardCore.Inventory.Core): Reacts once a change to a stock item is committed.
- `IStockLevelObserver` (CrestApps.OrchardCore.Inventory.Core): Hears about every change to a stock item's available units while the change is being made, before it is committed.
- `IPurchaseOrderStore` (CrestApps.OrchardCore.Inventory.Core)
- and 2 more contracts in the same projects.

## Documentation

See [Inventory](../../CrestApps.Docs/docs/modules/inventory.md) in the technical manual.
