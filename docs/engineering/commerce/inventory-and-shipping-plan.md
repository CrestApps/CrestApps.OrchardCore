# Inventory and shipping plan

Status: built (IS0 to IS9). Decisions agreed 2026-10-17 (Mike chose inventory first, no live carrier in this phase, and availability with a low-stock count for shoppers; the rest stand as proposed). Phases 6 and 7 of the [e-commerce plan](../../ecommerce-plan.md), after
Customers (#919 to #936) and Addresses (#927 to #932).

## Goal

Let a store sell physical goods properly:

- **Stock:** know how many of each product or variant are on hand, never sell the last unit twice, and refuse
  or allow backorders per product.
- **Shipping charges:** charge for shipping at checkout from rates the store sets per zone, taxed like any other
  line.
- **Fulfillment:** ship an order, in one or several shipments, with a carrier and tracking number the customer
  can follow.

Digital goods and services skip both: they hold no stock and need no shipping.

## Where things stand

| Piece | Today |
| --- | --- |
| Inventory, shipping, fulfillment | Nothing. The Carts and Orders manual lists them under *Not included*. |
| Products | `ProductPart.Sku`; `ProductType` per content type (`Good`, `Service`, `Digital`); variants with their own `VariantId` and optional SKU. **No weight or dimensions.** "Can be bought" is decided by `DefaultProductSnapshotResolver`. |
| Carts | Lines are re-priced through the snapshot resolver on every read, with a per-line `MaxQuantity` and `Problem`. Carts never touch stock. |
| Orders | Statuses `Draft`, `AwaitingPayment`, `Paid`, `Canceled`. A draft order is made when the buyer starts checkout (`CartCheckoutController` → `IOrderService.CreateFromCartAsync`), and `OrderCheckoutHandler.CompletingAsync` marks it paid. **An order whose checkout is canceled, fails or expires stays `AwaitingPayment` for ever**; only a newer checkout from the same cart cancels it. |
| Checkout | Handler stages Activating to Completed, plus Failed. `CancelAsync` and `ExpireAsync` call no handlers. Steps are added by handlers (Orders' concealed *Order* step carries the lines; Customers adds *Contact*). Extra charges are billing items on a step, followed by `CheckoutInvoiceBuilder.BuildAsync`, as the coupon step does. |
| Taxation | Lines carry a `TaxableItemKind` (including `Shipping`), category and classification codes. Tax rules already have `AppliesToShipping`. **But order checkouts send no destination address**: `DefaultCheckoutTaxProfileProvider` sets none, so address-based tax never applies to orders. |
| Addresses | International `Address` with region codes, address formats, the shared editor, and Customers' address book with a default shipping address. |
| Concurrency | `IDistributedLock` per aggregate (checkout session, coupon, customer); concurrency-checked YesSql catalogs; transactions keep an append-only event list. |

Found while surveying, fixed as part of this plan: paying for an order removes the bought cart lines by product and
price but not by variant (`OrderCheckoutHandler.RemoveBoughtLinesAsync`), so buying the blue mug also removes the red
one.

## Decisions (agreed 2026-10-17)

Foundation decision F5 already settles three: inventory is location-aware with a single default location for now;
stock is reserved when the order is created, with a hold that expires, and backorder is a per-product flag; shipping
rates come through a provider-neutral contract (flat, table, free, carrier) that returns taxable lines.

| # | Proposal | Why |
| --- | --- | --- |
| IS-D1 | **Close abandoned orders.** When an order's checkout is canceled, fails for good or expires, the order becomes `Canceled` (with a reason), through new `CanceledAsync` and `ExpiredAsync` handler stages. A sweep cancels `AwaitingPayment` orders whose session is gone. | Reservations need an end. Today an abandoned order never closes. |
| IS-D2 | **Stock is tracked per product, or per variant when the product has variants**, and only when the product type asks for it (an *Inventory* part, added to a type's goods). A stock item is keyed by product item id, variant id and location; the SKU is how merchants import, export and search it. | Variants are what customers pick. Not every store counts stock, and services and downloads have none. |
| IS-D3 | **A ledger of movements.** Every change is an immutable movement (received, adjusted, sold, returned, counted) with who, why and when; the stock item caches on hand and reserved, and can be rebuilt from the movements. | Audit and recovery: the exit gate says inventory can be rebuilt. |
| IS-D4 | **Reserve when the order is created, sell when it is paid.** Starting checkout reserves each tracked line under a per-item lock, refusing the checkout when there isn't enough; paying turns the reservations into *sold* movements; canceling or expiring releases them. A hold lasts as long as the checkout session (with a site setting, default 60 minutes). | F5. Reserving at the cart would let shoppers lock up stock just by browsing. |
| IS-D5 | **Out of stock is refused by default**, with *Allow backorders* per product (sell below zero, shown as *Backorder* to the buyer). Preorder waits. | The safe default. |
| IS-D6 | **Shoppers see availability, not counts**: *In stock*, *Only N left* below a threshold you set (default 5), or *Out of stock*, on product pages, cards and the cart; listings can filter to in-stock products. | Exact counts help competitors and can mislead while carts hold stock. |
| IS-D7 | **Physical products get shipping data**: weight on the product and on each variant (a site unit, kg or lb), and an optional *shipping class* (such as *Bulky*). Dimensions wait for carrier rates. | Weight-based rates need it; classes let one product cost more to ship. |
| IS-D8 | **Shipping zones by country and region**, using the Addresses codes, ordered so the first match wins, with a *Rest of world* fallback. Postal-code ranges wait. | Covers most stores without duplicating any country list. |
| IS-D9 | **Rate methods: flat rate (per order, optionally per item), weight or price table, and free above an order amount**, each per zone, with optional per-class surcharges. All go through `IShippingRateProvider`, so a carrier module can add live rates later. **No live carrier in this phase.** | Live carrier rates need paid accounts and their own module; the contract keeps the door open. |
| IS-D10 | **A Shipping checkout step** for orders with physical lines: the shipping address (from the address book for customers, entered for guests, defaulting to the billing address) and the method, with prices from the providers. The server recalculates the charge on every step and at payment. Orders without physical lines skip it. | One step, the same address editor, and no client-trusted prices. |
| IS-D11 | **Shipping is a taxed line, and the shipping address becomes the tax destination** (billing address when nothing ships), through an order tax profile provider. | Fixes the missing destination too. Taxation already knows shipping lines. |
| IS-D12 | **Shipments, not just a status.** An order has shipments, each with lines and quantities (partial shipments allowed), carrier, tracking number and link, and a status (*Pending*, *Shipped*, *Delivered*, *Returned*); the order shows *Unfulfilled*, *Partially fulfilled* or *Fulfilled*. Marking a shipment shipped emails the customer, and workflow events fire for each change. Labels and returns (RMA) are extension points only. | Real stores ship in parts; customers want tracking. |

## Design

### Modules and features

- **`CrestApps.OrchardCore.Inventory`** (feature *Inventory*, depends on Products and Orders):
  `Inventory.Abstractions` and `Inventory.Core` hold the models, store and services.
- **`CrestApps.OrchardCore.Shipping`** (feature *Shipping*, depends on Orders, Addresses and Checkout):
  `Shipping.Abstractions` (rate provider contract, `ShippingRateRequest`, `ShippingRate`) and `Shipping.Core`.
  *Fulfillment* (shipments) is part of Shipping; a store that only charges shipping still records shipments.
- Neither depends on the other. When both are on, a shipment can record the location it ships from.

### Inventory

- **`StockItem`** (YesSql, `Inventory` collection, concurrency-checked): `StockItemId`, `ContentItemId`,
  `VariantId`, `LocationId` (`default`), `Sku`, `OnHand`, `Reserved`, `Available` (computed),
  `LowStockThreshold`, `AllowBackorder`, `ModifiedUtc`. Index on item, variant, location, SKU and available.
- **`StockMovement`** (append-only): `StockItemId`, `Quantity` (signed), `Kind` (`Received`, `Adjusted`, `Sold`,
  `Returned`, `Counted`), `Reason`, `OrderId`, `ActorId`, `Utc`.
- **`StockReservation`**: `ReservationId`, `StockItemId`, `OrderId`, `CheckoutSessionId`, `Quantity`, `ExpiresUtc`,
  `Status` (`Held`, `Committed`, `Released`), `ReleaseReason`.
- **`IInventoryService`**: `GetAvailabilityAsync(product, variant)`, `ReserveAsync(order)` (all lines or none,
  each item under `STOCK_{id}`), `CommitAsync(order)`, `ReleaseAsync(order, reason)`, `AdjustAsync(item, delta,
  reason)`, `SetCountAsync(item, count)`, `RebuildAsync(item)`.
- **Hooks:** an order checkout handler reserves at Activating (refusing the checkout when short), commits at
  Completing (same transaction as *Paid*), and releases at Canceled, Failed and Expired; a sweep releases holds past
  `ExpiresUtc`.
- **Cart and product pages:** the snapshot resolver asks an `IProductAvailabilityProvider` (Inventory provides
  one), so the cart marks a line *Out of stock* or caps it at what is left, and product shapes show availability.
- **Admin:** *Commerce > Inventory*: a list with search by SKU or title, filters (low, out, backorder), adjust and
  count dialogs, and the movement history per item. Low stock raises a notification and a workflow event.
- **Import and export:** a CSV of SKU, on hand and threshold; recipe and deployment steps.

### Shipping

- **Product data:** a *Shipping* part on goods (weight, shipping class) and weight per variant.
- **`ShippingZone`**: name, position, countries, optional regions per country. **`ShippingMethod`**: zone, name,
  provider (`Flat`, `Table`, `Free`), settings (amounts, table rows by weight or subtotal, free-above amount),
  class surcharges, tax category, enabled.
- **`IShippingRateProvider.GetRatesAsync(ShippingRateRequest)`**: destination address, lines (weight, class,
  quantity, price), currency, subtotal; returns `ShippingRate` options (method id, name, amount, delivery estimate
  text).
- **Checkout:** a `ShippingCheckoutHandler` adds the *Shipping* step after *Contact* when the order has goods; its
  driver renders the address editor (or the address book picker) and the rates; choosing one adds a billing item
  with `TaxableKind = Shipping` and rebuilds the invoice. Paying copies `ShippingAddress`, `ShippingMethod` and
  `ShippingTotal` onto the order.
- **Tax:** `OrderCheckoutTaxProfileProvider` sets the destination to the shipping address, else the billing address.
- **Fulfillment:** `Shipment` records on the order (lines, quantities, carrier, tracking number and URL, status,
  dates). Admin *Fulfill* on the order page, *Mark shipped* and *Mark delivered*; the customer gets a shipped email
  with the tracking link, and My Orders and the guest order page show shipments.

## Slices

| Slice | Scope | Exit gate |
| --- | --- | --- |
| **IS0 Order lifecycle** | Canceled, Failed and Expired reach handlers; abandoned orders cancel with a reason; sweep; the variant fix in `RemoveBoughtLinesAsync` | An expired checkout cancels its order; buying one variant leaves the others in the cart. |
| **IN1 Stock and ledger** | Inventory module, part, `StockItem`, movements, admin list, adjust and count, rebuild | An administrator receives 10 mugs, sells none, counts 9, and the history explains the difference. |
| **IN2 Reservations** | Reserve at checkout start, commit on paid, release on cancel or expiry, holds sweep, concurrency tests | Two buyers racing for the last unit: one pays, the other is told it sold out. |
| **IN3a Availability** | Availability provider, cart limits, product page and card display, variant picker | Out-of-stock products can't be bought, unless backorders are allowed. |
| **IN3b Stock operations** | In-stock listing filter (search index field, reindex on stock change), low-stock notification and workflow event, CSV import and export, recipe and deployment steps | A listing filtered to in-stock products hides a product that just sold out; a CSV round trip restores counts. |
| **SH1 Zones and rates** | Shipping module, product shipping data, zones, methods, rate providers, admin | Rates for a US and a Canadian address differ as configured. |
| **SH2 Checkout** | Shipping step, shipping line, tax destination, order fields, emails and order pages | A physical order charges and taxes shipping; a digital-only order skips the step. |
| **SH3 Fulfillment** | Shipments, partial fulfillment, tracking, shipped email, workflow events, customer views | An order shipped in two parts shows both, and the customer has both tracking links. |
| **IS9 Docs and sample** | Manuals, the store sample with stock, zones and rates, changelog | A fresh store sample sells, ships and runs out. |

Each slice is a PR with tests, opened when its tests pass.

## As built

### IS0 Order lifecycle

- Checkout: `ICheckoutHandler.CanceledAsync(CheckoutFlowCanceledContext)` (with the reason) and
  `ExpiredAsync(CheckoutFlowExpiredContext)`, no-ops in `CheckoutHandlerBase`. `DefaultCheckoutEngine` raises them after
  committing the new status, inside the session lock, then commits again; `FailAsync` now commits what `Failed`
  handlers wrote too.
- Orders: `Order.CanceledUtc`, `Order.CancelReason` (`OrderCancelReason`: Replaced, PaymentFailed, CheckoutCanceled,
  CheckoutExpired, Abandoned); `IOrderService.CancelAsync` (draft or awaiting payment only) calling
  `IOrderEventHandler.CanceledAsync`; `OrderCheckoutHandler` cancels on Failed, Canceled and Expired when the order
  still names that checkout; `AbandonedOrderSweeper` plus `AbandonedOrdersBackgroundTask` every 15 minutes (orders
  over an hour old whose session ended, is missing or never existed; 500 per run). No index change: `OrderQuery`
  already filters by status and creation date.
- `RemoveBoughtLinesAsync` matches the variant. The order page shows why an order was canceled.
- Kept: an order replaced by a newer one can still be paid by its own open checkout (existing behavior). IN2 must
  handle a paid order whose reservation was already released.

### IN1 Stock and ledger

- `Inventory.Core` holds `InventoryPart`, `StockItem`, `StockMovement` and `InventorySettings`, with `StockItemIndex`
  and `StockMovementIndex` in the `Inventory` collection. It also holds the concurrency-checked `StockItemStore`, the
  append-only `StockMovementStore`, `IInventoryService` (Adjust, SetCount, Rebuild under `STOCK_{id}`, committing
  inside the lock) and `InventoryProductSynchronizer`.
- No separate Abstractions project yet: nothing outside Inventory needs the contract until IN3's availability
  provider, which belongs in Products.Core.
- Module: the content handler notes products and syncs them in a deferred task against their latest version, so drafts
  can be stocked. Tracking is decided by the type's definition, so products saved before the part was added are
  counted. *Find products to count* and a threshold change run `SyncAllAsync`.
- The product editor shows availability per variant, read-only; stock changes only under Commerce > Inventory, so every
  change is a movement.
- Found live: the variant editor is filled by script, so saving a product by a scripted form post would lose its
  variants. Live tests used the inventory pages and settings instead.

### IN2 Reservations

- `StockReservation` (Held, Committed, Released) with its index (upgrade step 2, indexes via
  `IndexTableIndexCreation`).
- `IInventoryService` gains `ReserveAsync` (all or nothing), `CommitAsync` (sells holds, and sells anyway when no hold is
  left; idempotent per order and item), `ReleaseAsync` and `ReleaseExpiredAsync`.
- Orders: `IOrderEventHandler.CreatedAsync` (can refuse, which cancels the order as `Unavailable` and returns
  `OrderCreationResult.Problems`) and `PaidAsync` (in the completion's unit of work), plus `OrderEventHandlerBase`. The
  cart checkout commits the order before starting the checkout and turns a `ConcurrencyException` into "try again".
- `InventoryOrderEventHandler` reserves, sells and releases. `ExpiredStockHoldsBackgroundTask` runs every 5 minutes.
  Settings gain *Hold stock for (minutes)*.
- Design change found live: IN1's service committed its own work inside a distributed lock, first in the request
  session, then (tried for IN2) in a separate session. A separate session deadlocks SQLite, which allows one writer,
  against the request's open transaction ("database is locked"). Committing the request session mid-request detaches
  documents read earlier (a duplicate-row trap). So the service now works only in the caller's session, with no locks
  and no commits of its own; the concurrency-checked `StockItem` makes a racing second commit fail. The admin
  actions and the cart checkout commit explicitly and catch the conflict, and the engine's Canceled and Expired paths
  roll back and log it, leaving the work to the sweeps.

### IN3a Availability

- Products.Core: `IProductAvailabilityProvider` (`ProductStockKey`, `ProductAvailabilityRequest` with `CartId`,
  `ProductAvailability` and its `Status`), and extensions to ask every provider and to combine variants.
- Inventory: `InventoryAvailabilityProvider`, which adds back the units a cart's own unpaid orders hold, so a buyer
  returning from checkout isn't told their items sold out.
- Carts: `CartService` caps quantities and refuses out-of-stock lines (`LimitedByStock` picks the message). The Add to
  cart display shows the label per variant, and the variant picker keeps the line and the button in step.
- Listing cards get availability through the Add to cart action they already render; the in-stock *filter* is IN3b.

### IN3b Stock operations

- Stock levels: `DefaultInventoryService` tells `IStockLevelObserver`s of every change to available units; the module's
  tracker defers to `StockLevelDispatcher`, which re-reads the items after commit and calls `IStockLevelHandler`s only
  for a changed level (in stock, low, out). A rolled-back change raises nothing.
- Handlers: a notification to the roles in `InventorySettings.LowStockNotificationRoles` (default Administrator), the
  `LowStockEvent` workflow event, and a search refresh when a product sells out or comes back.
- Listings: Products gains `ProductAttributeKeys.InStock` and `IProductSearchIndexRefresher`; Inventory contributes the
  value to each record. The automatic *In stock* toggle hides itself without a value, so sites without Inventory don't
  see it. Contributors are part of the index fingerprint, and `BuildVersion` went to 9.
- CSV export and import on Commerce > Inventory (`StockCsv`, `StockCountImporter`): counts, found by ids or SKU, all or
  nothing. The `Stock` recipe step and deployment step use the same importer.

### SH1 Zones and rates

- `Shipping.Core`: `ShippingZone` in a named catalog (one document, no index), holding its `ShippingMethod`s; provider
  settings live in each method's property bag (`FlatRateSettings`, `TableRateSettings`, `FreeShippingSettings`), so a
  carrier module adds its own provider and a `DisplayDriver<ShippingMethod>` without a schema change. No separate
  Abstractions project yet, as with Inventory.
- `IShippingRateService` finds the zone (first by position, then name; rest of world last) and prices its enabled
  methods; methods in another currency are skipped, class surcharges are per unit and matched ignoring case (a
  dictionary read back from JSON loses its comparer), and amounts are rounded to the currency.
- Zones store regions as ISO 3166-2 codes (`US-AK`); `Address.Region` is the code without the country.
- `ShippingPart` keeps per-variant weights by variant id; new variants get a row once the product is saved.
- Admin: Commerce > Shipping (zones in try order), a zone page listing its methods, a method editor per provider, and
  *Try rates*. Settings: weight unit and shipping classes. `ShippingZones` recipe and deployment step.

### SH2 Checkout

- `CheckoutShippingInfo` in Checkout.Abstractions carries where and how the order ships; the default tax profile provider
  uses its address as the destination, else the billing address. No separate order tax profile provider was needed.
- The Contact step rebuilds the invoice, so orders that don't ship are taxed where they're billed.
- `Order.ShippingAddress` and `Order.Shipping` (`OrderShipping`), copied by `OrderCheckoutHandler` like the contact info;
  anonymizing keeps country and region.
- The step: an address posted without a rate for it comes back with that address's rates as a validation error, so the
  checkout controller needed no new action. One rate is chosen automatically.
- Shipping depends on Orders now; the address book comes from Customers when it's on.
- Not done: re-pricing a chosen rate at payment. The price is fixed when chosen.

### SH3 Fulfillment

- `Shipment` documents in a `Shipping` collection with `ShipmentIndex` (migration step 2, indexes through
  `IndexTableIndexCreation`); `IShipmentService` and `ShippingOrderRateService` moved to Core so shipments tell goods
  from the rest.
- `Order.FulfillmentStatus`, set on every shipment change; returned shipments free their units.
- Orders gained an extension point: the order is built with `IDisplayManager<Order>` in `DetailAdmin` and `Detail`
  display types, which Shipping fills with the shipments card. No new order index.
- `IShipmentEventHandler` drives the shipped email (once per shipment) and four workflow events. Carriers with
  tracking templates live in the Shipping settings, with a new **Ship orders** (`FulfillOrders`) permission.

### IS9 Docs and sample

- The online store sample enables Inventory and Shipping, adds the Inventory and Shipping parts to its product type,
  weights by kind of product (boots *Bulky*), and `Stock`, `ShippingZones` and `Settings` (carriers) steps: 25 of most
  products, a few at 2 to 4, and one sold out; United States, Canada and rest-of-world zones.
- Manuals: inventory and shipping user and technical pages were written slice by slice; IS9 adds the sample to them, the
  product listings page and the changelog.

## Not in this phase

Multiple locations in the admin (the schema allows them), preorders, live carrier rates and labels, returns (RMA)
beyond the extension point, postal-code zones, dimensional weight, and pick lists.
