# Inventory and shipping plan

Status: decisions agreed 2026-10-17 (Mike chose inventory first, no live carrier in this phase, and availability with a low-stock count for shoppers; the rest stand as proposed). Phases 6 and 7 of the [e-commerce plan](../../ecommerce-plan.md), after
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
| **IN3 Availability** | Availability provider, cart and product display, backorders, low-stock notice, listing filter, CSV and recipe steps | Out-of-stock products can't be bought, unless backorders are allowed. |
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

## Not in this phase

Multiple locations in the admin (the schema allows them), preorders, live carrier rates and labels, returns (RMA)
beyond the extension point, postal-code zones, dimensional weight, and pick lists.
