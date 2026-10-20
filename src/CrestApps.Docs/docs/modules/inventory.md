---
sidebar_label: Inventory
title: Inventory
description: Stock items per product, variant and location, an append-only ledger of stock movements, and the service that changes stock under a per-item lock.
user_manual:
  - user-manual/commerce/inventory
  - user-manual/commerce/headless-api
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Inventory`, `CrestApps.OrchardCore.Inventory.StockAlerts` (Back-in-Stock Alerts), `CrestApps.OrchardCore.Inventory.PurchaseOrders` (Purchase Orders), `CrestApps.OrchardCore.Inventory.Api` (Inventory API) |
| **Core** | `CrestApps.OrchardCore.Inventory.Core` |
| **Category** | Commerce |
| **Depends on** | Commerce, Products, `OrchardCore.Contents` |

The **Inventory** feature counts the units of each product and variant on hand. The counts live outside the content item, as stock items. Every change is a movement in a ledger, and the stock item's count can be rebuilt from the movements.

## Model

All of these live in the `Inventory` YesSql collection.

- **`StockItem`** (concurrency-checked): `ContentItemId`, `VariantId` (null for a product without variants), `LocationId`, `Sku`, `Title`, `OnHand`, `Reserved`, `Available`, `LowStockThreshold`, `AllowBackorder`, `ReleaseDateUtc`, `Archived`, plus created and modified dates.
  - One stock item covers every location. Its units per location are in `Levels` (`StockLocationLevel`: `LocationId`, `OnHand`, `Reserved`, `Available`), and `OnHand` and `Reserved` are their totals. `GetLevel(locationId)` and `FindLevel(locationId)` read a location's units (`null` is the default location, `InventoryConstants.DefaultLocationId`, `default`). An item counted before there were locations has all its units at the default location: `NormalizeLevels()` puts any difference between the totals and the levels there, and `SumLevels()` sets the totals after a level changed.
  - `Available` is `OnHand` minus `Reserved`.
  - `ReleaseDateUtc` comes from the product's **Ships from** date and makes a backorder a pre-order until then.
  - `StockItemIndex` maps the product, variant and location, the normalized SKU and title, `OnHand`, `Available`, `IsLow` (`Available <= LowStockThreshold`), `AllowBackorder` and `Archived`.
- **`StockMovement`** (append-only): `StockItemId`, `Kind`, `Quantity` (signed), `OnHandAfter`, `Reason`, `OrderId`, `LocationId` (`null` for the default location), `PurchaseOrderId`, `ActorId`, `ActorName`, `Utc`. `StockMovementIndex` maps the stock item, kind, order and time.
  - `Kind` is one of `Received`, `Adjusted`, `Counted`, `Sold`, `Returned` or `Transferred`. Sold and returned movements come with checkout reservations; a transfer is a movement out of one location and one into the other.
- **`InventoryPart`** (attachable): `TrackInventory` (default true), `AllowBackorder`, `LowStockThreshold` (null for the site default), `ReleaseDateUtc` (the **Ships from** date, kept as noon UTC on the day so it reads as the same day in any time zone).
- **`StockReservation`**: `StockItemId`, `OrderId`, `LocationId`, `Quantity`, `ReturnedQuantity` (units of the sale returned so far), `Status` (`Held`, `Committed`, `Released`), `ExpiresUtc`, `ReleaseReason`. `StockReservationIndex` maps the stock item, order, status and expiry. A committed reservation is also what keeps an order's sale from being recorded twice.
- **`InventorySettings`** (Settings › Commerce › Inventory): `DefaultLowStockThreshold` (5), `HoldMinutes` (60) and `Locations` (up to 50 `InventoryLocation`: `LocationId`, `Name`, `Address` for staff), in the order orders take units from them. `GetLocations()` always includes the default location, named *Main location* until it's renamed.

## Services

- **`IInventoryService`** returns a `StockChangeResult`. Its error is `NotFound`, `InvalidQuantity`, `Busy` (someone else changed the same item at the same moment) or `NotEnoughAtLocation` (a transfer of more than the location has available, or to itself).
  - `AdjustAsync(stockItemId, kind, quantity, reason, actor)` takes `Received` with a positive quantity, or `Adjusted` with any non-zero quantity. `AdjustAsync(stockItemId, locationId, kind, …)` does it at a location.
  - `SetCountAsync(stockItemId, count, reason, actor)` records a `Counted` movement holding the difference. A count that matches is recorded too, so the history shows when stock was last checked. `SetCountAsync(stockItemId, locationId, count, …)` counts one location.
  - `TransferAsync(stockItemId, fromLocationId, toLocationId, quantity, reason, actor)` moves available units between locations; the total on hand doesn't change.
  - `ReceiveAsync(stockItemId, locationId, quantity, purchaseOrderId, reason, actor)` records units of a purchase order received at a location.
  - `RebuildAsync(stockItemId)` sets `OnHand` to the sum of the item's movements.
- **Where units come from.** An order's units of an item come from one location that has them all: the one the order asked for (`StockRequest.LocationId`, such as a pickup shop's stock location), then the locations in their listed order, then any location no longer listed that still holds units. Failing that, from each in turn; units no location has (a backorder) are owed by the first. Units held before the buyer chose where the order is fulfilled from are sold from that location when it has them, and units go back to the location they were sold from.
- **`IInventoryLocationService.GetLocationsAsync()`** lists the locations. `InventoryLocationService` also implements the Orders module's `IFulfillmentLocationProvider`, so Shipping offers the locations as a pickup location's **Stock comes from**.
- **Reservations.**
  - `ReserveAsync(orderId, lines, expiresUtc)` holds every counted line or none: a line short of available units, on an item that doesn't allow backorders, returns a `StockShortage` and holds nothing. Uncounted products are skipped, and an item the order already holds isn't held again.
  - `CommitAsync(orderId, lines)` sells what the order holds: `Reserved` and `OnHand` go down and a `Sold` movement records the order. A line with no hold left (expired, or the order was replaced) is sold anyway, and an item already sold for the order is skipped, so a retried completion sells nothing twice.
  - `ReleaseAsync(orderId, reason)` releases what the order still holds. `ReleaseExpiredAsync(take)` releases holds past `ExpiresUtc`.
- **Units of work.** Every method works in the caller's YesSql session and is committed with the caller's work.
  - Stock items are concurrency-checked: when two changes to one item race, the later commit throws `ConcurrencyException` and keeps nothing.
  - A separate unit of work isn't used. On SQLite, which allows one writer, it would wait for the caller's open transaction while the caller waits for it.
- **`InventoryProductSynchronizer.SyncAsync(contentItemId, product, typeHasInventory, settings)`** keeps a product's stock items in step with it:
  - one stock item per variant, or one for a product without variants;
  - each item gets the SKU (the variant's, else the product's), the title (`Product (Variant)`), the threshold (the product's, else the site default) and the backorder flag;
  - items of removed variants, and of products no longer tracked or gone, are archived, never deleted; a variant that comes back is unarchived.

  A product saved before its type had the part is counted with the part's defaults.
- **`InventoryContentHandler`** notes products as they are updated, published, imported or removed. A deferred task then has `InventorySyncService` sync them against their latest version, so stock can be counted before a product is published.
- **`InventorySyncService.SyncAllAsync()`** syncs every product whose type has the part. It runs from **Find products to count** and when the default threshold changes. `SyncAsync` returns whether a stock item was added or archived; the products for which one was have their search records refreshed through Products' `IProductSearchIndexRefresher`, when Product Listing is on.

## Stock levels

A stock item's level is `StockLevel.InStock` (more than the threshold available), `Low` (some, up to the threshold) or `Out` (none).

- **`IStockLevelObserver`** hears every change to an item's available units as `DefaultInventoryService` makes it, with the units available before. The module's `StockLevelTracker` notes the first one per item and schedules a deferred task.
- **`IStockLevelHandler`** is told after the work is committed, in a scope of its own. `StockLevelDispatcher` reads each item again and calls the handlers only when its level changed, so a change that was rolled back raises nothing, and one failing handler doesn't stop the others. `StockLevelChangedContext` has the item, `PreviousAvailable`, `PreviousLevel`, `Level`, and `RanLow` (it dropped to low or out), `SoldOut` and `BackInStock`.
- Handlers in the module:
  - `LowStockNotificationHandler` (with **Notifications**) sends `RanLow` changes to the enabled users of `InventorySettings.LowStockNotificationRoles`, at most 100.
  - `LowStockWorkflowHandler` (with **Workflows**) triggers `LowStockEvent` with the product as `ContentItem`, `StockItem`, `StockItemId`, `ContentItemId`, `VariantId`, `Sku`, `Title`, `Available`, `LowStockThreshold` and `SoldOut`, correlated by the stock item id.
  - `StockSearchIndexHandler` (with **Product Listing**) refreshes the product's search record when it sold out or came back.

## Listings

With Product Listing on, `InventorySearchDocumentContributor` writes the built-in attribute `ProductAttributeKeys.InStock` (`Product.InStock`) to each product's search record: `true` when any variant has available units, or no `IProductAvailabilityProvider` counts the product; `false` otherwise, backorders included. Listings offer it as the automatic *In stock* toggle filter (URL key `in-stock`), which shows only when a record says `true`, so it stays hidden on sites that don't count stock.

## Import, export and recipes

- **`StockCsv`** writes and reads the CSV: `Sku`, `Title`, `ContentItemId`, `VariantId`, `OnHand`, `Reserved`, `Available`, `LowStockThreshold`. Reading finds columns by name in any case; it needs `OnHand` and `Sku` or `ContentItemId`, and handles quoted values over several lines.
- **`StockCountImporter`** applies `StockCountRow`s as counts in the caller's unit of work. A row is matched by `ContentItemId` and `VariantId`, else by SKU (`IStockItemStore.FindBySkuAsync`, which finds nothing when two items share it). It returns how many were counted and unchanged, and a `StockCountProblem` per row it couldn't apply: `NotFound`, `InvalidCount`, `Duplicate` or `TooManyRows`.
- **Admin.** `inventory/export` downloads the CSV with a byte order mark; `inventory/import` (Manage inventory) applies a file of up to 2 MB and 10,000 rows, and discards every count when any row has a problem.
- **Recipe step `Stock`** (`StockStep`): `Items` with `Sku` or `ContentItemId` and `VariantId`, and `OnHand`. Products named by id that aren't counted yet are synced first. Rows it can't apply are recipe errors.

  ```json
  {
    "name": "Stock",
    "Items": [
      { "Sku": "MUG", "OnHand": 24 },
      { "ContentItemId": "4rgryp179mr10m6yvbt42xym8n", "VariantId": "4ydepe37jp6sn68kq7pvr4dh5d", "OnHand": 5 }
    ]
  }
  ```

- **Deployment step `Stock`** (`StockDeploymentStep`, *Commerce*) exports every item that isn't archived with its ids, SKU and `OnHand`.
- **Content Transfer.** `InventoryPartContentImportHandler` adds the `InventoryPart_*` columns: `TrackInventory`, `AllowBackorder`, `LowStockThreshold`, `ShipsFrom`, `Stock` (available, for a product without variants) and `VariantStock` (each variant's, by SKU or options, such as `TEE-S=4; M / Red=2`). Counts are set once the import has saved the product (`InventoryCountApplier`, in a deferred task after the stock items are synced), keeping the units unpaid orders hold on hand besides them.

## Orders

With Orders enabled, `InventoryOrderEventHandler` (an `IOrderEventHandler`) ties stock to orders:

- **Created:** reserves the order's lines for `HoldMinutes`. A shortage refuses the order with a message for the buyer: *Only 1 of Mug left. Lower the quantity to check out.* or *Mug is out of stock. Remove it to check out.*
- **Paid:** sells them in the checkout's own unit of work. A concurrent change to the same item rolls the completion back, and the checkout's reconciliation sweep retries it.
- **Canceled:** releases them, and when the store asks, puts back what a paid order sold.
- **Refunded:** when the store refunds everything and asks for it, puts back what the order sold.
- **Lines changed** (a store's draft order edited): releases the old hold and holds the new lines for `HoldMinutes`, all or none; a shortage refuses the change and keeps the old lines.
- **Returned** (the Orders module's Returns feature): puts the lines the store ticked back into stock, bundles as their parts, at the location chosen or where they were sold from.

`IInventoryService.RestockAsync(orderId, reason, actor)` returns an order's sold units to stock with a `Returned` movement per item, once: the reservation is marked `Returned`, so asking again changes nothing. `RestockAsync(orderId, lines, locationId, reason, actor)` returns some units, never more of an item than the order bought and hasn't returned yet (`StockReservation.ReturnedQuantity`), and returns how many went back.

`ExpiredStockHoldsBackgroundTask` releases expired holds every five minutes, up to 500 per run. The cart's **Checkout** commits the order, and what it holds, before the checkout starts, and turns a concurrency conflict into *Someone else is buying the same items right now.* An item's admin page lists the orders holding it.

## Reports

With the [Reports](reports) feature on, **Stock on hand** (under **Reports > Commerce**, permission `ViewInventory`) lists every counted item with its units on hand, held and available, out of stock and low first, with totals. It shows stock as it is when it runs.

## Availability

`InventoryAvailabilityProvider` implements Products' `IProductAvailabilityProvider`. It answers from the stock items, every location together: `Available`, `AllowBackorder` and `LowStockThreshold` for each product or variant, and `ReleaseDateUtc` while the item's release date is to come, which makes a backorder `ProductAvailabilityStatus.Preorder`. A product asked about as a whole gets each of its variants. When the request names a cart (`ProductAvailabilityRequest.CartId`), the units held by that cart's unpaid orders (`IOrderStore.ListUnpaidByCartAsync`) are added back, so a buyer's own hold counts as theirs. Products that aren't counted are left out of the answer and can always be bought.

With **Product Bundles** on, `BundleAvailabilityProvider` answers for bundles before the stock items: the most bundles the counted parts can make, from `ProductBundleIndex`, taking backorders when every counted part does. `InventoryOrderEventHandler` holds, sells and restocks a bundle line's parts (`OrderLine.BundleItems`) rather than the bundle, and `BundleSearchIndexHandler` refreshes the search records of the bundles a product is in when it sells out or comes back.

## API

The **Inventory API** feature (`CrestApps.OrchardCore.Inventory.Api`) maps into the commerce JSON API at `api/commerce/v1`:

- `GET products/{id}/availability`, for guests: the product's or a variant's stock status at each location, with the count only when it's low.
- `inventory` and `inventory/{stockItemId}` with **View inventory**, and `receive`, `adjust`, `count` and `transfer` under it with **Manage inventory**, through `IInventoryService`. Each change is committed in the request; a collision with a checkout changing the same stock answers `409` with the code `concurrency`.

See [Headless commerce API](../user-manual/commerce/headless-api.md) for the endpoints, the conventions they share, and how apps sign in.

## Admin

- **Commerce > Inventory** (`inventory`, `InventoryAdminController`) lists stock items, fewest available first.
  - Search matches the start of the SKU or title. Filters: *Low stock*, *Out of stock*, *Backordered*.
  - Each item's page (`inventory/{stockItemId}`) shows its counts and paged movement history, with forms to receive or adjust, count, and recount from history. With more than one location it shows the units by location, the receive, adjust and count forms ask for a location, and `inventory/{stockItemId}/transfer` moves units between them.
  - **Export CSV** and **Import CSV** download and apply counts.
- **Product editor.** The Inventory part sets the policy, and shows each variant's availability with a link to its page.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage inventory** | Changing stock, finding products to count, and the Inventory settings (registered for the `inventory` settings group, so it opens that page without **Manage settings**). Implies **View inventory**. |
| **View inventory** | Listing stock and reading its history. |

Administrators have both by default.

## Back-in-stock alerts

The **Back-in-Stock Alerts** feature (`CrestApps.OrchardCore.Inventory.StockAlerts`, needs `OrchardCore.Email`) keeps `StockAlert` records (`StockItemId`, `ContentItemId`, `VariantId`, `Email`, `UserId`, `CreatedUtc`, `SentUtc`; `StockAlertIndex` with `Waiting`) in the `Inventory` collection.

- `StockAlertDisplayDriver` renders the `StockAlertForm` shape in the `ProductAction` display of a product's own page, under **Add to cart**, when one of its counted items is out of stock and doesn't take backorders; for a product with variants it shows while a sold-out variant is picked.
- `POST stock/alerts` (`StockAlertSubscribe`) records a request through `StockAlertService.SubscribeAsync`, refusing an item that can be bought and taking a repeat of a waiting request as done. It's limited to 10 requests per email a day (`MaxPerEmailPerDay`) and 20 per IP an hour, and answers JSON when asked.
- `BackInStockAlertHandler`, an `IStockLevelHandler`, emails the item's waiting requests oldest first when `BackInStock`, up to 500 per restock (`MaxPerRestock`), and marks each sent. The product's link is built from the site's base URL.
- `StockAlertPersonalDataContributor` exports a person's requests (by email or user id) with their commerce data and deletes them when the data is anonymized.

## Purchase orders

The **Purchase Orders** feature (`CrestApps.OrchardCore.Inventory.PurchaseOrders`) adds **Commerce > Purchase orders**. A `PurchaseOrder` (`Inventory` collection, `PurchaseOrderIndex` on number, supplier, status, location and dates) has a `Number` (such as `PO-261019-K3QZ`), `Supplier`, `SupplierEmail`, the `LocationId` received at, `Currency`, `ExpectedUtc`, `Note`, `Lines` (`PurchaseOrderLine`: `StockItemId`, title, SKU, `Ordered`, `Received`, `UnitCost`) and a `Status`: `Draft`, `Ordered`, `PartiallyReceived`, `Received` or `Canceled`.

- `PurchaseOrdersAdminController` (`purchase-orders`, viewing with `ViewInventory`, changes with `ManageInventory`) lists them through an `AdminList` with a status filter and search by number or supplier, and creates (optionally from the location's low-stock items, each up to twice its threshold), edits, marks ordered, receives, cancels and deletes drafts. Lines can be added and removed only while it's a draft. Items are found through `Admin/api/crestapps/inventory/stock-items/search`.
- `PurchaseOrderReceiver.ReceiveAsync(order, quantities, actor)` receives units of its lines at its location through `IInventoryService.ReceiveAsync`, never more than are still to come, with the order's number as the movement's reason, and marks it received once nothing is.

The supplier isn't emailed.
