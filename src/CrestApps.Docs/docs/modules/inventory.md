---
sidebar_label: Inventory
title: Inventory
description: Stock items per product, variant and location, an append-only ledger of stock movements, and the service that changes stock under a per-item lock.
user_manual: user-manual/commerce/inventory
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Inventory` |
| **Core** | `CrestApps.OrchardCore.Inventory.Core` |
| **Category** | Commerce |
| **Depends on** | Commerce, Products, `OrchardCore.Contents` |

The **Inventory** feature counts the units of each product and variant on hand. The counts live outside the content item, as stock items. Every change is a movement in a ledger, and the stock item's count can be rebuilt from the movements.

## Model

All of these live in the `Inventory` YesSql collection.

- **`StockItem`** (concurrency-checked): `ContentItemId`, `VariantId` (null for a product without variants), `LocationId`, `Sku`, `Title`, `OnHand`, `Reserved`, `Available`, `LowStockThreshold`, `AllowBackorder`, `Archived`, plus created and modified dates.
  - `LocationId` is always `default` for now. The schema is location-aware, so several locations can come later without a breaking migration.
  - `Available` is `OnHand` minus `Reserved`.
  - `StockItemIndex` maps the product, variant and location, the normalized SKU and title, `OnHand`, `Available`, `IsLow` (`Available <= LowStockThreshold`), `AllowBackorder` and `Archived`.
- **`StockMovement`** (append-only): `StockItemId`, `Kind`, `Quantity` (signed), `OnHandAfter`, `Reason`, `OrderId`, `ActorId`, `ActorName`, `Utc`. `StockMovementIndex` maps the stock item, kind, order and time.
  - `Kind` is one of `Received`, `Adjusted`, `Counted`, `Sold` or `Returned`. Sold and returned movements come with checkout reservations.
- **`InventoryPart`** (attachable): `TrackInventory` (default true), `AllowBackorder`, `LowStockThreshold` (null for the site default).
- **`StockReservation`**: `StockItemId`, `OrderId`, `Quantity`, `Status` (`Held`, `Committed`, `Released`), `ExpiresUtc`, `ReleaseReason`. `StockReservationIndex` maps the stock item, order, status and expiry. A committed reservation is also what keeps an order's sale from being recorded twice.
- **`InventorySettings`** (Settings › Commerce › Inventory): `DefaultLowStockThreshold` (5) and `HoldMinutes` (60).

## Services

- **`IInventoryService`** returns a `StockChangeResult`. Its error is `NotFound`, `InvalidQuantity` or `Busy`, the last when someone else changed the same item at the same moment.
  - `AdjustAsync(stockItemId, kind, quantity, reason, actor)` takes `Received` with a positive quantity, or `Adjusted` with any non-zero quantity.
  - `SetCountAsync(stockItemId, count, reason, actor)` records a `Counted` movement holding the difference. A count that matches is recorded too, so the history shows when stock was last checked.
  - `RebuildAsync(stockItemId)` sets `OnHand` to the sum of the item's movements.
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

## Orders

With Orders enabled, `InventoryOrderEventHandler` (an `IOrderEventHandler`) ties stock to orders:

- **Created:** reserves the order's lines for `HoldMinutes`. A shortage refuses the order with a message for the buyer: *Only 1 of Mug left. Lower the quantity to check out.* or *Mug is out of stock. Remove it to check out.*
- **Paid:** sells them in the checkout's own unit of work. A concurrent change to the same item rolls the completion back, and the checkout's reconciliation sweep retries it.
- **Canceled:** releases them, and when the store asks, puts back what a paid order sold.
- **Refunded:** when the store refunds everything and asks for it, puts back what the order sold.

`IInventoryService.RestockAsync(orderId, reason, actor)` returns an order's sold units to stock with a `Returned` movement per item, once: the reservation is marked `Returned`, so asking again changes nothing.

`ExpiredStockHoldsBackgroundTask` releases expired holds every five minutes, up to 500 per run. The cart's **Checkout** commits the order, and what it holds, before the checkout starts, and turns a concurrency conflict into *Someone else is buying the same items right now.* An item's admin page lists the orders holding it.

## Availability

`InventoryAvailabilityProvider` implements Products' `IProductAvailabilityProvider`. It answers from the stock items at the default location: `Available`, `AllowBackorder` and `LowStockThreshold` for each product or variant. A product asked about as a whole gets each of its variants. When the request names a cart (`ProductAvailabilityRequest.CartId`), the units held by that cart's unpaid orders (`IOrderStore.ListUnpaidByCartAsync`) are added back, so a buyer's own hold counts as theirs. Products that aren't counted are left out of the answer and can always be bought.

## Admin

- **Commerce > Inventory** (`inventory`, `InventoryAdminController`) lists stock items, fewest available first.
  - Search matches the start of the SKU or title. Filters: *Low stock*, *Out of stock*, *Backordered*.
  - Each item's page (`inventory/{stockItemId}`) shows its counts and paged movement history, with forms to receive or adjust, count, and recount from history.
  - **Export CSV** and **Import CSV** download and apply counts.
- **Product editor.** The Inventory part sets the policy, and shows each variant's availability with a link to its page.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage inventory** | Changing stock, finding products to count, and the Inventory settings (registered for the `inventory` settings group, so it opens that page without **Manage settings**). Implies **View inventory**. |
| **View inventory** | Listing stock and reading its history. |

Administrators have both by default.

## Coming next

Shipping zones and rates, a shipping step at checkout, and shipments.
