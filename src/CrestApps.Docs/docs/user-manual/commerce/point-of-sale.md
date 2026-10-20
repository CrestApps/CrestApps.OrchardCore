---
sidebar_label: Point of sale
title: Point of sale
description: Sell at your stores' counters from a till app: set up registers and Stripe card readers, open and close shifts with their cash, ring up sales paid in cash, by card or a mix, print and email receipts, and take returns at the till.
technical_manual:
  - modules/point-of-sale
  - modules/payments
---

The **Point of Sale** feature lets your staff sell at the counter of each of your stores, from a phone, tablet or till computer running a **till app**. The till app talks to your site through the [commerce API](headless-api.md), so a sale at the counter uses the same products, prices, coupons, gift cards, stock and orders as your online store.

At each store you set up one or more **registers**. A member of staff opens a **shift** at a register with the cash in its drawer, rings up sales, takes cash, a card or both, prints or emails the receipt, and at the end of the day counts the drawer and closes the shift. You see every shift, what it sold and whether its drawer came out right.

| | |
| --- | --- |
| **Menu** | Commerce > Point of sale > Registers; Commerce > Point of sale > Shifts |
| **Permission** | **Sell at a register** for staff at the till; **Manage registers and shifts** to set up registers and read every shift |
| **Feature** | Point of Sale (it needs Orders, Inventory and the Checkout API). Receipts, to print and email receipts. Stripe, to take cards on Stripe Terminal readers. |

<AskYourAdmin />

## Before you start

1. Turn on **Point of Sale**, and **Receipts** if you'll print or email receipts.
2. Add a location for each store under **Settings > Commerce > Inventory**: a register sells from one of them. See [Inventory](inventory.md).
3. Give staff who sell at the counter a role with **Sell at a register**. Give whoever manages the stores **Manage registers and shifts**, which includes selling.
4. Set up sign-in for the till app, as an app people sign in to with the authorization code flow, and include the `roles` scope so registers can check staff roles. See [Set up sign-in](headless-api.md#set-up-sign-in).

Staff sign in to the till app with their own accounts, so every shift, sale and cash movement says who made it.

## Set up registers

1. Open **Commerce > Point of sale > Registers** and click **Add register**.
2. Fill in the fields (below), and click **Save**.

*The register was added. Staff who may sell at registers can open a shift at it from the till app.* confirms it.

| Field | What it does |
| --- | --- |
| **Name** | What staff see when they pick a register, such as *Downtown till 1*. Required. |
| **Store location** | The inventory location its sales take stock from. Required. |
| **Store address** | The store's street address, city and two-letter country code, such as *US*. Sales at the register are taxed by this address, and its receipts show it. Required. |
| **Currency** | The currency its drawer holds, such as *USD*. Leave it blank to sell in the products' own currency. |
| **Who may sell here** | Tick roles to let only their staff sell at this register. None ticked lets anyone with **Sell at a register**. Staff with **Manage registers and shifts** may sell at every register. |
| **On** | A register that's off can't open a shift or ring up a sale. |

## Card payments

A sale can be paid by card in two ways:

- **Card on the counter terminal.** The store's own card terminal, from your bank or another provider, charges the card. Staff then record the card payment in the till app, with the terminal's approval code. The site doesn't talk to the terminal: it records that the card paid.
- **Card on a Stripe reader.** With the Stripe feature connected, a Stripe Terminal reader at the register takes the card. The till app tells the site which reader to use, and the site sends the payment to it; the buyer taps, inserts or swipes their card. The app needs no Stripe software of its own.

### Pair a Stripe reader

A reader is paired once, by the code it shows when you set it up. A manager (**Manage registers and shifts**) pairs it from the till app:

```http
POST pos/terminal/readers
Content-Type: application/json

{
  "registrationCode": "sunny-river-moon",
  "label": "Downtown till 1",
  "address": { "addressLine1": "1 Main Street", "city": "Springfield", "region": "IL", "postalCode": "62701", "country": "US" }
}
```

Stripe keeps each reader at a location. Give the store's `address` and a Stripe location is made for it, or name one you made in the Stripe Dashboard with `stripeLocationId`. The answer is the reader:

```json
{ "id": "tmr_...", "label": "Downtown till 1", "deviceType": "bbpos_wisepos_e", "status": "online", "locationId": "tml_...", "action": null, "actionStatus": null }
```

`GET pos/terminal/readers` lists your readers, so the till app can let staff pick the one at their register.

:::tip[Try it without a reader]
With Stripe in test mode, pair a simulated reader with the registration code `simulated-wpe`. When a sale is waiting on it, `POST pos/terminal/readers/{readerId}/test-card` presents a test card, as if a buyer tapped one.
:::

## A day at the till

Here is a shift as a till app makes it, call by call. Paths are shown without `https://your-site/api/commerce/v1/`, every call carries the staff member's bearer token, and the answers leave out some fields to stay short. Send an `Idempotency-Key` on the calls that take one (see [Sending a request again safely](headless-api.md#sending-a-request-again-safely)), so a dropped connection never rings a sale up twice.

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

A discount code or a gift card goes on the sale's checkout with the [checkout endpoints](headless-api.md#checkout): `POST checkout/7c2m9x1k/codes` or `POST checkout/7c2m9x1k/gift-cards`. Store credit and loyalty points aren't spent at a register. To give up on a sale before it's paid, cancel its checkout with `DELETE checkout/7c2m9x1k`: the order is canceled and its stock let go.

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

- `GET pos/sales/4d1k8s0p2w/receipt` gives the receipt as a printable HTML page, branded from your [Receipts settings](invoices-and-receipts.md);
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

`GET pos/shifts/9s4m2k7x` gives the same, at any time while it's open, so the till can show how the day is going.

## The cash drawer

The drawer should hold:

- the **opening float**,
- plus the **cash kept** for sales: what buyers handed over, less the change given back,
- plus cash **put in**, less cash **taken out**, which includes cash handed back for returns.

That's the **expected** cash. At the close, the counted cash less the expected cash is the **difference**: negative when the drawer is short, positive when it's over.

## Shifts

**Commerce > Point of sale > Shifts** lists every shift, newest first, each **Open** or **Closed**, with when it opened and who opened it, and for a closed one its difference. The **Filter by register** list in the header shows one register's shifts. **Shifts**, on a register in the registers list, opens the same.

Click **View** to open a shift:

| Shown | What it means |
| --- | --- |
| **Sales** | How many sales the shift took. |
| **Total** | What they came to. |
| **Cash kept** | The cash kept for them: handed over, less the change. |
| **Card** | What cards paid, on the counter terminal or a Stripe reader. |
| **Gift cards and balances** | What gift cards paid. |
| **Refunded** | What has been refunded of the shift's sales, wherever it was refunded. |
| **Opening float** | The cash put in the drawer at the start. |
| **Put in, less taken out** | The cash movements, returns at the till included. Each one is listed with its reason and who made it. |
| **Expected** | What the drawer should hold. |
| **Counted** and **Difference** | For a closed shift: the cash counted, and how far off it was. Negative is shown in red. |

The note given at the close is shown with it.

## What a register sale does differently

A register sale is an order, like an online one, so it's under **Commerce > Orders**, in your reports, and in the buyer's account when you named a customer with an account. It differs in these ways:

- Its **channel** is *Point of sale*. The orders list filters by channel (**Every channel**, **Online**, **Point of sale**, **Drafts sent by staff**), and its CSV export follows the filter. A register sale shows a badge with its register.
- Its **location** is the register's store: its stock is held and sold there. When that store has none of an item, the stock comes off your main location.
- It records the **register**, the **shift** and the **staff member** who rang it up.
- The buyer takes it with them: nothing is shipped, and its checkout skips the **Contact** and **Shipping** steps.
- It's **taxed** by the register's store address, and billed to it.
- It's paid in **cash**, by **card on the counter terminal**, by **card on a Stripe reader**, with a **gift card**, or a mix. Store credit and loyalty points aren't taken at a register.

## Endpoint reference

All paths start with `api/commerce/v1/`, and every call needs a staff member's token.

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `pos/registers` | **Sell at a register** | Lists the registers the caller may sell at, each with its open shift. |
| `POST` | `pos/registers/{registerId}/shifts` | **Sell at a register** | Opens a shift with its `openingFloat`. Takes an idempotency key. |
| `GET` | `pos/shifts/{shiftId}` | **Sell at a register** | Gets a shift: what it's sold, how it was paid, and the cash its drawer should hold. |
| `POST` | `pos/shifts/{shiftId}/cash` | **Sell at a register** | Records cash put in (`amount` positive) or taken out (negative), with a `reason`. Takes an idempotency key. |
| `POST` | `pos/shifts/{shiftId}/close` | **Sell at a register** | Closes a shift with the `countedCash` and a `note`. |
| `POST` | `pos/sales` | **Sell at a register** | Rings up `lines` at a register in its open shift, for a `customerId`, a walk-in `customerEmail` and `customerName`, or nobody. Takes an idempotency key. |
| `POST` | `pos/sales/{checkoutId}/pay` | **Sell at a register** | Pays a sale with `cash`, then a `card` or a `reader` for what's left; `{}` asks whether a reader's payment is done. Takes an idempotency key. |
| `GET` | `pos/sales/{orderId}/receipt` | **Sell at a register** | Gets the receipt as a printable HTML page. Needs Receipts. |
| `POST` | `pos/sales/{orderId}/receipt/email` | **Sell at a register** | Emails the receipt to an `email`. Needs Receipts. |
| `POST` | `pos/returns` | **Sell at a register** | Refunds a sale at a register, all of it or an `amount`, with a `reason`; `restock` puts the items back. Takes an idempotency key. |
| `GET` | `pos/terminal/readers` | **Sell at a register** | Lists the Stripe Terminal readers. Needs Stripe. |
| `POST` | `pos/terminal/readers` | **Manage registers and shifts** | Pairs a reader by its `registrationCode`, with a `label`, at a `stripeLocationId` or a store `address`. Needs Stripe. |
| `POST` | `pos/terminal/readers/{readerId}/test-card` | **Sell at a register** | In Stripe test mode, presents a test card on a simulated reader. |

The registers and shifts endpoints answer only for registers the caller may sell at; another register answers `404`. The conflict codes they add are `shift-open` (the register already has an open shift) and `shift-closed` (open a shift first, or the shift is closed).

## Related pages

- [Headless commerce API](headless-api.md)
- [Carts and orders](carts-and-orders.md)
- [Inventory](inventory.md)
- [Gift cards](gift-cards.md)
- [Invoices and receipts](invoices-and-receipts.md)
- [Payment providers](payment-providers.md)
