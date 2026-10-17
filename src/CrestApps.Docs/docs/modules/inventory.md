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
- **`InventorySettings`** (Settings › Commerce › Inventory): `DefaultLowStockThreshold`, 5 by default.

## Services

- **`IInventoryService`** returns a `StockChangeResult`. Its error is `NotFound`, `InvalidQuantity` or `Busy`, the last when the lock wasn't acquired.
  - `AdjustAsync(stockItemId, kind, quantity, reason, actor)` takes `Received` with a positive quantity, or `Adjusted` with any non-zero quantity.
  - `SetCountAsync(stockItemId, count, reason, actor)` records a `Counted` movement holding the difference. A count that matches is recorded too, so the history shows when stock was last checked.
  - `RebuildAsync(stockItemId)` sets `OnHand` to the sum of the item's movements.
- **Locking.** Each change takes the distributed lock `STOCK_{stockItemId}`, reads the item, writes the movement and the item, and commits, all while holding the lock.
- **`InventoryProductSynchronizer.SyncAsync(contentItemId, product, typeHasInventory, settings)`** keeps a product's stock items in step with it:
  - one stock item per variant, or one for a product without variants;
  - each item gets the SKU (the variant's, else the product's), the title (`Product (Variant)`), the threshold (the product's, else the site default) and the backorder flag;
  - items of removed variants, and of products no longer tracked or gone, are archived, never deleted; a variant that comes back is unarchived.

  A product saved before its type had the part is counted with the part's defaults.
- **`InventoryContentHandler`** notes products as they are updated, published, imported or removed. A deferred task then has `InventorySyncService` sync them against their latest version, so stock can be counted before a product is published.
- **`InventorySyncService.SyncAllAsync()`** syncs every product whose type has the part. It runs from **Find products to count** and when the default threshold changes.

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

Reserving stock when checkout starts and selling it when the order is paid, availability on product pages and in the cart, backorders at checkout, low-stock notifications, and CSV, recipe and deployment steps.
