---
sidebar_label: Point of Sale
title: Point of Sale
description: Registers at inventory locations, shifts with a counted cash drawer, counter sales as orders on the checkout framework with a cash tender and a counter-card payment method, receipts, returns at the till, and the point-of-sale API a till app calls, with Stripe Terminal readers.
user_manual: user-manual/commerce/point-of-sale
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.PointOfSale` (Point of Sale) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders, Inventory, Checkout API |
| **Works with** | Receipts (printed and emailed receipts), Customers (a named buyer), Gift Cards, Stripe (Terminal readers) |

<YouTube video="point-of-sale" title="Narrated video overview of Point of sale" />

The **Point of Sale** feature sells at a store's counter from a till app over the [commerce API](commerce-api.md). A sale is an order on the ordinary checkout framework, so prices, coupons, gift cards, tax, stock holds, order events and reports work as they do online. The module adds what a counter needs: registers, shifts with their cash, a cash tender, a payment method for the store's own card terminal, receipts, and returns at the till.

<YouTube video="point-of-sale-till" title="Narrated video of a day at the till and what the manager sees" />

## Model

Registers and shifts live in the `PointOfSale` YesSql collection.

- **`Register`** (`RegisterIndex`): `Name`, `LocationId` (an inventory location), `Address` (an `Address`, which sales are taxed by and billed to), `Currency` (empty sells in the products' own currency), `Roles` (empty lets anyone who may sell at a register), `IsEnabled`, `CreatedUtc`, `ModifiedUtc`.
- **`RegisterShift`** (`RegisterShiftIndex`): `RegisterId`, `RegisterName`, `LocationId`, `Currency`, `Status` (`Open` or `Closed`), who opened and closed it and when, `OpeningFloat`, `CashMovements` (each a `RegisterCashMovement`: `Amount`, positive in and negative out, `Reason`, who and when), and at the close `CountedCash`, `ExpectedCash`, `CloseNote` and `Summary`.
- **`RegisterShiftSummary`**: `Sales`, `Total`, `Cash` (kept: handed over less change), `Card`, `Other` (gift cards and other balances) and `Refunded` (refunds of the shift's sales). `PosShiftService.SummarizeAsync` works it out from the shift's paid orders (`OrderQuery.ShiftId`); a closed shift keeps it.

`RegisterShift.GetExpectedCash(cashKept)` is the float, plus the cash kept for sales, plus the cash movements.

## Orders and the checkout

`PosSaleService.StartAsync` prices the lines as a cart (`ICartService.PriceAsync`, without saving a cart), and makes the order with `IOrderService.CreateFromCartAsync`, setting:

| Order field | Value |
| --- | --- |
| `Channel` | `OrderChannels.PointOfSale` (`pos`) |
| `LocationId` | The register's location, which Inventory holds and sells the stock at (`InventoryOrderEventHandler`, falling back to the main location for an item the store has none of) |
| `RegisterId`, `RegisterName`, `ShiftId` | The register and its open shift |
| `StaffUserId`, `StaffName` | Who rang it up |
| `CustomerId`, `UserId`, customer name, email and phone | The buyer, when one is named |
| `BillingAddress` | The register's address |

It commits that, then starts the order's checkout with `ICheckoutEngine.StartAsync` and puts on the session:

- **`CheckoutCounterSale`** (`RegisterId`, `ShiftId`, `LocationId`), which marks it a counter sale;
- a `CheckoutShippingInfo` for an *In store* rate of zero at the register's address, so the invoice is taxed where it's sold;
- the buyer's `CheckoutContactInfo`, when they have an email.

`OrderChannels.IsCounterSale(order)` makes the Customers' **Contact** step and Shipping's **Shipping** step drop out. Store credit and loyalty points aren't offered for a counter sale. The session belongs to the signed-in staff member who started it.

### Paying

`PosSaleService.PayAsync` pays in this order:

1. **Cash.** `PosCashTenderProvider` (`ICheckoutTenderProvider`, key `cash`) is a balance whose reference is the cash handed over in the currency's minor units (`PosCashTenderProvider.ToReference`). It pays up to what's due, after tax like a gift card, and only on a session with `CheckoutCounterSale`. The change is what was handed over less what the cash tenders paid (`PosSaleService.GetChange`).
2. **A card**, for what's left: `PosCounterCardPaymentProvider` (`ICheckoutPaymentProvider` and `ICheckoutPaymentRefundProvider`, key `pos-card`, *Card on the counter terminal*) records a card the store's own terminal charged; its reference is the approval code given (`pos_card_` and the code), and it verifies as paid at once. A refund through it records the refund; staff make it on the terminal. Or `StripeTerminalPaymentProvider` (key `stripe-terminal`, below), given a reader's id.
3. **Completion.** `ICheckoutEngine.TryCompleteAsync`. A reader payment answers pending until Stripe reports the PaymentIntent succeeded.

### Receipts

`PosReceipts.ToApi` gives a receipt as data (`ApiPosReceipt`: lines, totals, payments, change) in the completed payment's answer. With the Receipts feature on, `PosReceipts.RenderAsync` builds an `IReceiptService` document (source *Point of sale*, the payments in its notes) and renders it with `IReceiptHtmlRenderer`, for `GET pos/sales/{orderId}/receipt` and its email through `IEmailService`.

### Returns

`POST pos/returns` refunds a paid order through `OrderOperationsService.RefundAsync` (to how it was paid, restocking at the order's location unless `restock` is `false`). The refund goes to the card first and to the sale's cash last, so the cash handed back is what the refund put back on the sale's cash tenders (read from `ICheckoutTenderLedger` before and after): it's recorded as a negative cash movement on the register's open shift, which a return requires. Only register sales are returned at a till, unless the caller has **Manage orders**.

## API

`PosApiEndpoints` maps these into the commerce JSON API at `api/commerce/v1`, under the tag **Point of Sale**. Each needs a signed-in caller who may sell at the register concerned (`PosShiftService.MaySellAtAsync`: the register is on, the caller has **Sell at a register**, and is in one of its roles or has **Manage registers and shifts**); another register answers `404`.

| Method | Path | What it does |
| --- | --- | --- |
| `GET` | `pos/registers` | The registers the caller may sell at (`ApiRegister`, with `openShiftId`). |
| `POST` | `pos/registers/{registerId}/shifts` | Opens a shift (`openingFloat`); `409 shift-open` when one is open. Idempotent. |
| `GET` | `pos/shifts/{shiftId}` | The shift (`ApiShift`), with its summary and expected cash. |
| `POST` | `pos/shifts/{shiftId}/cash` | A cash movement (`amount`, `reason`); `409 shift-closed`. Idempotent. |
| `POST` | `pos/shifts/{shiftId}/close` | Closes it with `countedCash` and `note`. |
| `POST` | `pos/sales` | Rings up `lines` (`PosSaleLine`: `productId`, `variantId`, `priceId`, `quantity`, `addOns`) for a `customerId`, or a `customerEmail` and `customerName`; `201` with `orderId`, `orderNumber` and the `checkout` (`ApiCheckout`); `400` on `customerId` when it isn't a customer, `409 shift-closed` without an open shift. Idempotent. |
| `POST` | `pos/sales/{checkoutId}/pay` | `cash`, then `card` (`approvalCode`) or `reader` (`readerId`); answers `ApiPosPayment` with `status` `completed`, `due` or `pending`, `amountDue`, `change` and, once completed, `receipt`; `{}` asks whether a reader's payment is done. `409 shift-closed` once the sale's shift has closed; `409 not-available` for a declined reader payment. Idempotent. |
| `GET` | `pos/sales/{orderId}/receipt` | The receipt's HTML; `409 unsupported` without Receipts. |
| `POST` | `pos/sales/{orderId}/receipt/email` | Emails it to `email`; `204`. |
| `POST` | `pos/returns` | `orderId`, `registerId`, `amount`, `reason`, `restock`; answers `refunded`, `cashBack` and the refund's `messages`. Idempotent. |

Codes and gift cards go on the sale's checkout through the [checkout endpoints](checkout#the-commerce-api), and `DELETE checkout/{id}` cancels an unpaid sale.

## Stripe Terminal

With the [Stripe](payments) feature and Point of Sale both on, Stripe's `TerminalStartup` adds `StripeTerminalPaymentProvider` (`ICheckoutPaymentProvider` and `ICheckoutPaymentRefundProvider`, key `stripe-terminal`, *Card on a Stripe reader*), offered only for a session with `CheckoutCounterSale`. It uses Stripe's server-driven integration, so the till app needs no Stripe SDK:

- **Begin** creates a `card_present` PaymentIntent for the attempt's amount and tax (idempotency key `terminal-pi-` and the attempt id, or the attempt's existing PaymentIntent when begun again) and tells the reader in `ProviderData["readerId"]` to process it.
- **Verify** reads the PaymentIntent: `succeeded` is paid, `canceled` failed, anything else still waiting.
- **Cancel** cancels the PaymentIntent; **refund** goes through `IStripeRefundService`.

`StripeTerminalApiEndpoints` adds, under the same tag:

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `pos/terminal/readers` | Sell at a register | Lists the account's readers (`ApiStripeReader`: `id`, `label`, `deviceType`, `status`, `locationId`, `action`, `actionStatus`). |
| `POST` | `pos/terminal/readers` | Manage registers and shifts | Pairs a reader by `registrationCode`, with a `label`, at `stripeLocationId` or a Stripe location made from `address`. `simulated-wpe` pairs a simulated reader in test mode. |
| `POST` | `pos/terminal/readers/{readerId}/test-card` | Sell at a register | In test mode, presents a test card on a simulated reader (`TestHelpers.Terminal.ReaderService.PresentPaymentMethodAsync`); `409 unsupported` on a live account. |

### Pair a reader

A manager pairs a reader once, by the registration code it shows when it's set up:

```http
POST pos/terminal/readers
Content-Type: application/json

{
  "registrationCode": "sunny-river-moon",
  "label": "Downtown till 1",
  "address": { "addressLine1": "1 Main Street", "city": "Springfield", "region": "IL", "postalCode": "62701", "country": "US" }
}
```

Stripe keeps each reader at a Stripe location: give the store's `address` and one is made for it, or name one made in the Stripe Dashboard with `stripeLocationId`. The answer is the reader:

```json
{ "id": "tmr_...", "label": "Downtown till 1", "deviceType": "bbpos_wisepos_e", "status": "online", "locationId": "tml_...", "action": null, "actionStatus": null }
```

`GET pos/terminal/readers` lists the readers, so the till app can let staff pick the one at their register. In test mode, the registration code `simulated-wpe` pairs a simulated reader, and `POST pos/terminal/readers/{readerId}/test-card` presents a test card on it while a sale is waiting.

## A shift, call by call

A shift as a till app makes it. Paths are shown without `https://your-site/api/commerce/v1/`, every call carries the staff member's bearer token, and the answers leave out some fields. Send an `Idempotency-Key` on the calls that take one (see [Idempotency keys](commerce-api.md#idempotency-keys)), so a dropped connection never rings a sale up twice.

### 1. Pick a register and open a shift

```http
GET pos/registers
```

```json
[
  { "id": "4r9k2x7m1q", "name": "Downtown till 1", "locationId": "downtown", "currency": "USD", "address": { "city": "Springfield", "country": "US" }, "openShiftId": null }
]
```

The list has only the registers the staff member may sell at. `openShiftId` names the register's open shift, when it has one: carry on with it. Otherwise open one with the cash put in the drawer, the **float**:

```http
POST pos/registers/4r9k2x7m1q/shifts
Content-Type: application/json

{ "openingFloat": 100.00 }
```

The answer is `201 Created` with the shift. A register has one open shift at a time; opening another answers `409` with the code `shift-open`.

### 2. Ring up a sale

```http
POST pos/sales
Content-Type: application/json

{
  "registerId": "4r9k2x7m1q",
  "lines": [ { "productId": "4b2kx7m9qz1c", "quantity": 2 } ],
  "customerEmail": "avery@example.com",
  "customerName": "Avery Brooks"
}
```

Each line is a product, with `variantId`, `priceId` and `addOns` as for adding it to a cart. Name the buyer with `customerId` when staff look them up, so the sale is on their account and earns them points (an id that isn't a customer answers `400`); or give a walk-in buyer's `customerEmail` and `customerName`, for their receipt; or neither.

The answer is `201 Created`:

```json
{
  "orderId": "4d1k8s0p2w",
  "orderNumber": "1043",
  "checkout": {
    "id": "7c2m9x1k",
    "status": "pending",
    "totals": {
      "subtotal": { "amount": 24.0, "currency": "USD" },
      "tax": { "amount": 1.98, "currency": "USD" },
      "total": { "amount": 25.98, "currency": "USD" },
      "amountDue": { "amount": 25.98, "currency": "USD" }
    }
  }
}
```

The sale is an order, with its stock held at the register's store while it's paid. A sale can't be rung up without an open shift (`409`, `shift-closed`), and a product that can't be sold, or has run out, answers `409` with the code `not-available` and what's wrong.

A discount code or a gift card goes on the sale's checkout with the [checkout endpoints](commerce-api.md#checkout): `POST checkout/7c2m9x1k/codes` or `POST checkout/7c2m9x1k/gift-cards`. Store credit and loyalty points aren't spent at a register. To give up on a sale before it's paid, `DELETE checkout/7c2m9x1k` cancels its checkout: the order is canceled and its stock let go.

The sale's checkout belongs to the staff member who rang it up, so the same account pays it.

### 3. Take the payment

Pay with the cash the buyer hands over:

```http
POST pos/sales/7c2m9x1k/pay
Content-Type: application/json

{ "cash": 30.00 }
```

```json
{
  "status": "completed",
  "amountDue": { "amount": 0.0, "currency": "USD" },
  "change": { "amount": 4.02, "currency": "USD" },
  "receipt": {
    "orderNumber": "1043",
    "staff": "jordan",
    "lines": [ { "description": "Contoso mug", "quantity": 2, "unitPrice": { "amount": 12.0, "currency": "USD" }, "total": { "amount": 24.0, "currency": "USD" } } ],
    "total": { "amount": 25.98, "currency": "USD" },
    "payments": [ { "method": "Cash", "amount": { "amount": 25.98, "currency": "USD" } } ],
    "change": { "amount": 4.02, "currency": "USD" }
  }
}
```

| `status` | What it means |
| --- | --- |
| `completed` | Paid, and the order is placed. Hand back `change`, and print the `receipt`. |
| `due` | `amountDue` is still to pay: take more cash, or a card. |
| `pending` | A Stripe reader is waiting for the card. Ask again in a couple of seconds by paying with `{}`. |

A sale is paid in the shift it was rung up in: once that shift has closed, paying answers `409` with the code `shift-closed`, and the sale is rung up again on the open shift.

When more cash is handed over than is due, the rest is the change. Send all the cash the buyer hands over in one `cash`. Some cash and a card for the rest is one call, or two:

```http
POST pos/sales/7c2m9x1k/pay

{ "cash": 20.00, "card": { "approvalCode": "483920" } }
```

`card` records a card taken on the counter terminal, for what's left after the cash; take it on the terminal first. `reader` sends what's left to a Stripe reader instead:

```http
POST pos/sales/7c2m9x1k/pay

{ "reader": { "readerId": "tmr_..." } }
```

The answer is `pending` until the buyer presents their card; then paying again with `{}` answers `completed`. If the card is declined, paying answers `409` with the code `not-available`: send the payment to the reader again, or take another way to pay.

### 4. The receipt

The answer that completes the sale holds the receipt as data, for a till app that prints in its own layout on a receipt printer. With the **Receipts** feature on:

- `GET pos/sales/4d1k8s0p2w/receipt` gives the receipt as a printable HTML page, branded from the [Receipts](receipts.md) settings;
- `POST pos/sales/4d1k8s0p2w/receipt/email` with `{ "email": "avery@example.com" }` emails it.

Without Receipts, both answer `409` with the code `unsupported`.

### 5. Cash in and out

Cash put in the drawer or taken out outside a sale, such as more change from the safe or a banking run, is recorded on the shift: a positive amount is put in, a negative one taken out.

```http
POST pos/shifts/9s4m2k7x/cash
Content-Type: application/json

{ "amount": -200.00, "reason": "Banked at noon" }
```

### 6. Returns at the till

```http
POST pos/returns
Content-Type: application/json

{ "orderId": "4d1k8s0p2w", "registerId": "4r9k2x7m1q", "reason": "Wrong size", "restock": true }
```

```json
{
  "orderId": "4d1k8s0p2w",
  "orderNumber": "1043",
  "refunded": { "amount": 25.98, "currency": "USD" },
  "cashBack": { "amount": 25.98, "currency": "USD" },
  "messages": [ "..." ]
}
```

Leave out `amount` to refund all of it, or give an `amount` to refund part. The items go back on the shelf at the register's store unless `restock` is `false`. What the sale paid in cash is handed back from the drawer, `cashBack`, and recorded as cash taken out of the register's open shift, so a return needs an open shift (`409`, `shift-closed`). When a sale was paid partly in cash and partly by card, the card is refunded first and only what it doesn't cover comes out of the drawer. A till returns its store's register sales; an online order is returned there only by staff with **Manage orders**. The rest goes back to how it was paid: a Stripe reader's card is refunded through Stripe, and a gift card gets its money back. A card taken on the counter terminal is refunded on that terminal: the site records the refund, and staff make it on the terminal.

### 7. Close the shift

At the end of the shift, count the cash in the drawer, and close it:

```http
POST pos/shifts/9s4m2k7x/close
Content-Type: application/json

{ "countedCash": 124.50, "note": "Short 1.48, a coin dropped behind the till" }
```

```json
{
  "id": "9s4m2k7x",
  "registerName": "Downtown till 1",
  "status": "closed",
  "openingFloat": { "amount": 100.0, "currency": "USD" },
  "sales": 1,
  "total": { "amount": 25.98, "currency": "USD" },
  "cash": { "amount": 25.98, "currency": "USD" },
  "card": { "amount": 0.0, "currency": "USD" },
  "expectedCash": { "amount": 125.98, "currency": "USD" },
  "countedCash": { "amount": 124.5, "currency": "USD" },
  "difference": { "amount": -1.48, "currency": "USD" }
}
```

`GET pos/shifts/9s4m2k7x` gives the same at any time while it's open, so the till can show how the day is going.

## Admin

- **Commerce > Point of sale > Registers** (`point-of-sale/registers`, `RegistersAdminController`) lists registers through the `AdminList` shape, with **Add register** and each register's editor.
- **Commerce > Point of sale > Shifts** (`point-of-sale/shifts`) lists shifts, newest first, with a register filter in the list toolbar; a shift's page shows its sales by tender and its cash drawer (float, put in less taken out, expected, counted, difference), its cash movements and its close note.

## Permissions

| Permission | Allows |
| --- | --- |
| **Sell at a register** (`SellAtRegister`) | The point-of-sale API at the registers the user's roles may use: shifts, sales, payments, receipts and returns. |
| **Manage registers and shifts** (`ManageRegisters`) | The registers and shifts admin pages, selling at every register, and pairing Stripe readers. |

Administrators have both by default.

## Report Builder data source

With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) on, which the Report Builder needs, Point of Sale adds the **Point of Sale** data source (`PointOfSale`) to the Report Builder and to Orchard Core data pipelines. Both data sets need **Manage registers and shifts** (`ManageRegisters`), the permission of the registers and shifts admin pages. The orders data set's register and shift IDs reference these data sets, so a report can join a sale to its register and shift.

The **Registers** data set (`Registers`) has one row per register, newest first by when it was added; it has no default date. Joins on the register and location IDs and filters on the register's name are applied in the database, and simple groups and totals of the fields its index holds (name, location and enabled) are counted there too. The store's street address is never exposed, only its country, region, city and postal code.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Register ID | Identifies the register. |
| `Name` | Register | |
| `IsEnabled` | Enabled | |
| `LocationId` | Location ID | References `Inventory` > `Locations`. |
| `Currency` | Currency | Empty when the register sells in the products' own currency. |
| `Roles` | Roles that may sell | The role names, separated by commas; empty when anyone who may sell at a register may sell here. |
| `CreatedUtc` | Added | |
| `ModifiedUtc` | Last changed | |
| `Country`, `Region`, `City`, `PostalCode` | Country, Region, City, Postal code | From the store's address. |

The **Register shifts** data set (`RegisterShifts`) has one row per shift, newest first by when it was opened, which is its default date. Date ranges on that date and joins on the shift, register and location IDs are applied in the database, and simple groups and totals of the register, location and opening date are counted there too. Money is in the shift's currency, which the `Currency` field names: the register's, or the store's when the register has none. The note left at the close and the reasons given for cash put in or taken out are never exposed.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Shift ID | Identifies the shift. |
| `RegisterId` | Register ID | References `PointOfSale` > `Registers`. |
| `RegisterName` | Register | |
| `LocationId` | Location ID | References `Inventory` > `Locations`. |
| `Status` | Status | `Open` or `Closed`. |
| `OpenedUtc` | Opened | The default date. |
| `ClosedUtc` | Closed | |
| `OpenedByUserId`, `ClosedByUserId` | Opened by user ID, Closed by user ID | Reference the users (`Users`). |
| `OpenedByName`, `ClosedByName` | Opened by, Closed by | The staff member's name. |
| `Currency` | Currency | The currency of every money field of the shift. |
| `OpeningFloat` | Opening float | The cash put in the drawer at the start. |
| `CashMovementCount` | Cash movements | How many times cash was put in or taken out outside a sale, refunds at the till included. |
| `CashIn` | Cash put in | |
| `CashOut` | Cash taken out | A positive amount, refunds at the till included. |
| `ExpectedCash` | Expected cash | Empty while the shift is open. |
| `CountedCash` | Counted cash | Empty while the shift is open. |
| `CashDifference` | Difference | The cash counted less the cash expected: negative when the drawer was short. |
| `SaleCount` | Sales | Kept when the shift closes; empty while it is open, like the sales fields below. |
| `SalesTotal` | Sales total | |
| `CashSales` | Cash kept | The cash handed over for sales less the change given. |
| `CardSales` | Card | |
| `OtherSales` | Gift cards and balances | |
| `RefundedAmount` | Refunded | Refunds of the shift's sales. |

## Related modules

- [Carts and Orders](carts-and-orders#sales-channels): the order's channel, location, register and shift.
- [Inventory](inventory): the locations registers sell from.
- [Checkout](checkout): the checkout a sale is paid through, and balances that pay after tax.
- [Payments](payments#stripe-terminal-readers): Stripe Terminal readers.
- [Receipts](receipts): the receipt's layout.
