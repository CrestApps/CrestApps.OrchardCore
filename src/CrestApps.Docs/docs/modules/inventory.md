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

- **`IInventoryService`** returns a `StockChangeResult`. Its error is `NotFound`, `InvalidQuantity` or `Busy`, the last when the lock wasn't acquired.
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
- **`InventorySyncService.SyncAllAsync()`** syncs every product whose type has the part. It runs from **Find products to count** and when the default threshold changes.

## Orders

With Orders enabled, `InventoryOrderEventHandler` (an `IOrderEventHandler`) ties stock to orders:

- **Created:** reserves the order's lines for `HoldMinutes`. A shortage refuses the order with a message for the buyer: *Only 1 of Mug left. Lower the quantity to check out.* or *Mug is out of stock. Remove it to check out.*
- **Paid:** sells them in the checkout's own unit of work. A concurrent change to the same item rolls the completion back, and the checkout's reconciliation sweep retries it.
- **Canceled:** releases them.

`ExpiredStockHoldsBackgroundTask` releases expired holds every five minutes, up to 500 per run. The cart's **Checkout** commits the order, and what it holds, before the checkout starts, and turns a concurrency conflict into *Someone else is buying the same items right now.* An item's admin page lists the orders holding it.

## Availability

`InventoryAvailabilityProvider` implements Products' `IProductAvailabilityProvider`. It answers from the stock items at the default location: `Available`, `AllowBackorder` and `LowStockThreshold` for each product or variant. A product asked about as a whole gets each of its variants. When the request names a cart (`ProductAvailabilityRequest.CartId`), the units held by that cart's unpaid orders (`IOrderStore.ListUnpaidByCartAsync`) are added back, so a buyer's own hold counts as theirs. Products that aren't counted are left out of the answer and can always be bought.

## Admin

- **Commerce > Inventory** (`inventory`, `InventoryAdminController`) lists stock items, fewest available first.
  - Search matches the start of the SKU or title. Filters: *Low stock*, *Out of stock*, *Backordered*.
  - Each item's page (`inventory/{stockItemId}`) shows its counts and paged movement history, with forms to receive or adjust, count, and recount from history.
- **Product editor.** The Inventory part sets the policy, and shows each variant's availability with a link to its page.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage inventory** | Changing stock, finding products to count, and the Inventory settings (registered for the `inventory` settings group, so it opens that page without **Manage settings**). Implies **View inventory**. |
| **View inventory** | Listing stock and reading its history. |

Administrators have both by default.

## Coming next

An in-stock filter for listings, low-stock notifications, and CSV, recipe and deployment steps.
