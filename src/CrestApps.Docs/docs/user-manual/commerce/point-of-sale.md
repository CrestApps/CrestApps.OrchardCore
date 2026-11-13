---
sidebar_label: Point of sale
title: Point of sale
description: "Sell at your stores' counters from the web register or a till app: set up registers, departments and Stripe card readers, open and close shifts with their cash, ring up items and amounts by department, take cash, a card or a mix, print and email receipts, and take returns at the till."
technical_manual:
  - modules/point-of-sale
  - modules/payments
---

The **Point of Sale** feature lets your staff sell at the counter of each of your stores. They sell in the browser, on the site's own **web register**, or from a phone, tablet or till computer running a **till app** that your developers, or a partner, build on the [commerce API](headless-api.md). Either way, a sale at the counter uses the same products, prices, coupons, gift cards, stock and orders as your online store.

A store can also sell **by department**, the way a cash register does: key in $10.00 and press **Candy**, and $10.00 counts toward candy sales, taxed as candy, with no item to look up and no stock to count. That suits a small store that rings up most sales by amount.

At each store you set up one or more **registers**. A member of staff opens a **shift** at a register with the cash in its drawer, rings up sales, takes cash, a card or both, prints or emails the receipt, and at the end of the day counts the drawer and closes the shift. You see every shift, what it sold and whether its drawer came out right.

| | |
| --- | --- |
| **Menu** | Commerce > Point of sale > Web register; Registers; Shifts; Departments |
| **Permission** | **Sell at a register** for staff at the till; **Refund at a register** for staff who take returns; **Manage registers and shifts** to set up registers and departments, and read every shift |
| **Feature** | Point of Sale (it needs Orders, Inventory and the Checkout API). Point of Sale Web Register, to sell in the browser. Receipts, to print and email branded receipts. Stripe, to take cards on Stripe Terminal readers. |

<AskYourAdmin />

This video shows why a store sells at the counter with the same system, the registers and shifts screens, a day at the till, and register sales in your orders:

<YouTube video="point-of-sale" title="Narrated video overview of Point of sale" />

## Before you start

1. Turn on **Point of Sale**; **Point of Sale Web Register** to sell in the browser; and **Receipts** if you'll email receipts or print them branded.
2. Add a location for each store under **Settings > Commerce > Inventory**: a register sells from one of them. See [Inventory](inventory.md).
3. Give staff who sell at the counter a role with **Sell at a register**. Give whoever manages the stores **Manage registers and shifts**, which includes selling.
4. For the web register, give those roles **Access admin panel** too: the web register is a page of the admin.
5. For a till app, set up its sign-in, as an app people sign in to with the authorization code flow, and include the `roles` scope so registers can check staff roles. See [Set up sign-in](headless-api.md#set-up-sign-in).

Staff sign in with their own accounts, so every shift, sale and cash movement says who made it.

## Set up registers

<YouTube video="point-of-sale" title="Video of setting up a register" start={135} end={206} />

1. Open **Commerce > Point of sale > Registers** and click **Add register**.
2. Fill in the fields (below), and click **Save**.

*The register was added. Staff who may sell at registers can open a shift at it from the web register or a till app.* confirms it.

| Field | What it does |
| --- | --- |
| **Name** | What staff see when they pick a register, such as *Downtown till 1*. Required. |
| **Store location** | The inventory location its sales take stock from. Required. |
| **Store address** | The store's street address, city and two-letter country code, such as *US*. Sales at the register are taxed by this address, and its receipts show it. Required. |
| **Currency** | The currency its drawer holds, such as *USD*. Leave it blank to sell in the products' own currency. |
| **Who may sell here** | Tick roles to let only their staff sell at this register. None ticked lets anyone with **Sell at a register**. Staff with **Manage registers and shifts** may sell at every register. |
| **On** | A register that's off can't open a shift or ring up a sale. |
| **Department keys** | The keys on its keypad that ring an amount up in a department. See [Program the department keys](#program-the-department-keys). |

## Departments

A department is what a register rings an amount up in when there's no item to track, such as *Grocery*, *Candy* or *Lottery*. Its sales are counted toward it, on the shift report and in the shift's page, and taxed by its tax category. Departments are only for the registers: they never show in your online store.

1. Open **Commerce > Point of sale > Departments** and click **Add department**.
2. Fill in the fields (below), and click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | What its key, receipts and reports say, such as *Candy*. Required. |
| **Number** | The department's number, such as *4*, if your store numbers them. No two departments can share one. |
| **Tax category** | What its sales are taxed as, from your [tax categories](taxes.md), such as groceries at a lower rate. Leave it on **The store's default rate** to tax them like anything else. |
| **Position** | Where its key comes on registers whose keys you haven't programmed: a smaller number first. |
| **On** | Registers can't ring amounts up in a department that's off, and drop its key. |
| **Key color** | The color of its key on the register. |
| **Least amount** and **Most amount** | The limits of one ring-up. The most amount catches a mistyped amount, such as 1000.00 for 10.00. |

**Delete** removes a department; its past sales keep its name.

### Program the department keys

Each register shows a key for every department that's on, in their order, until you program its keys. To choose its keys, open the register under **Commerce > Point of sale > Registers**, and in **Department keys**:

1. Click **Add key**, and choose its department.
2. Optionally, change what the key reads, and give it a **Preset amount**: a key with one rings that amount up when nothing is keyed in, such as *1.00* for a coffee.
3. Use the arrow to move a key up, and the bin to remove it. Keys show in this order.
4. Click **Save**.

## The web register

With **Point of Sale Web Register** on, staff sell in the browser at **Commerce > Point of sale > Web register**. Staff who may sell at more than one register pick theirs first.

1. **Open the shift.** Enter the cash you put in the drawer, and click **Open shift**.
2. **Ring up.** Key in an amount on the keypad or the keyboard, cents first, so *1 0 0 0* is 10.00, and press a department. A key with a preset amount rings it up alone. To ring up several, key in how many, press **×**, then the amount and the department: *3 × 1 5 0* then **Candy** is three at 1.50. To sell an item, scan its barcode, or type its SKU and press Enter. Click a line and **Void line** to take it off, or **Void ticket** to start again.
3. **Take the payment.** Click **Total and pay**: the site adds the tax. Enter the cash handed over, or click one of the quick amounts, and **Take cash**: the change is shown. For a card, charge it on your card terminal, then click **Card paid**, with the approval code if you like. Cash and a card can share a sale. **Back to the ticket** voids the sale so you can change it.
4. **Print the receipt**, then click **New sale**. With Receipts on, the receipt is your branded one; otherwise it's a plain till receipt for an 80 mm receipt printer.
5. **Cash in or out** records cash put in the drawer or taken out, with the reason. **Shift report** shows how the shift is going: sales, tax, how they were paid, what the drawer should hold, and what each department rang up.
6. **Close the shift.** Count the drawer, float included, enter it, and click **Close shift**. The closing report shows the count against what the drawer should hold.

The web register doesn't name a customer, take discount codes or gift cards, use Stripe readers, or take returns. A till app does those.

## Card payments

A sale can be paid by card in two ways:

- **Card on the counter terminal.** The store's own card terminal, from your bank or another provider, charges the card. Staff then record the card payment in the till app, with the terminal's approval code. The site doesn't talk to the terminal: it records that the card paid.
- **Card on a Stripe reader.** With the Stripe feature connected, a Stripe Terminal reader at the register takes the card. The till app tells the site which reader to use, and the site sends the payment to it; the buyer taps, inserts or swipes their card. The app needs no Stripe software of its own.

### Pair a Stripe reader

A reader is paired once, by the registration code it shows when you set it up. A manager (**Manage registers and shifts**) pairs it from the till app: they enter the code, a label such as *Downtown till 1*, and the store's address. Stripe keeps each reader at a location: the site makes one for the store's address, or uses a location you made in the Stripe Dashboard. Once paired, the reader appears in the till app's list of readers, so staff can pick the one at their register.

:::tip[Try it without a reader]
With Stripe in test mode, pair a simulated reader with the registration code *simulated-wpe*. When a sale is waiting on it, the till app can present a test card on it, as if a buyer tapped one.
:::

Your developers find the requests the till app sends in [Point of Sale](../../modules/point-of-sale.md#pair-a-reader), in the Technical Manual.

This video follows a day at the till, step by step, and shows what each step leaves for the manager in the admin:

<YouTube video="point-of-sale-till" title="Narrated video of a day at the till and what the manager sees" />

## A day at the till

<YouTube video="point-of-sale-till" title="Video of a day at the till, step by step" start={51} end={112} />

Here is a shift as staff work it in the till app. Each step is a call the till app makes to your site; the Technical Manual walks through the same day [call by call](../../modules/point-of-sale.md#a-shift-call-by-call). A till app built as the Technical Manual describes never rings a sale up, or takes a payment, twice, even when its connection drops.

### 1. Pick a register and open a shift

The staff member signs in and picks their register. The list shows only the registers they may sell at. If the register already has an open shift, they carry on with it. Otherwise they open one, entering the cash put in the drawer, the **float**.

A register has one open shift at a time: opening a second one is refused.

### 2. Ring up a sale

The staff member adds the products the buyer is taking, with their variant, price and add-ons as on the product page. They can name the buyer:

- **Look up a customer**, so the sale is on their account and earns them points. A customer who can't be found is refused, so the sale is never put on the wrong account.
- **Enter a walk-in buyer's name and email**, for their receipt.
- **Or name nobody.**

The sale is an order, with its stock held at the register's store while it's paid. A sale can't be rung up without an open shift, and a product that can't be sold, or has run out, is refused with what's wrong.

A discount code or a gift card goes on the sale before it's paid. Store credit and loyalty points aren't spent at a register. To give up on a sale before it's paid, cancel it: the order is canceled and its stock let go.

The staff member who rang up the sale is the one who takes its payment.

### 3. Take the payment

The buyer can pay:

- **In cash.** Enter all the cash the buyer hands over: when it's more than is due, the site works out the change to hand back.
- **By card on the counter terminal.** Charge the card on the terminal first, then record it in the till app with the terminal's approval code.
- **By card on a Stripe reader.** The till app sends what's due to the reader, and waits while the buyer taps, inserts or swipes their card. If the card is declined, the payment is refused: send the payment to the reader again, or take another way to pay.
- **With a mix**: some cash, and a card for the rest.

When nothing is left to pay, the sale is complete and the order is placed. If there's still something to pay, the till app is told what's due, so staff can take more cash or a card.

A sale is paid in the shift it was rung up in. Once that shift has closed, the sale can't be paid any more: ring it up again on the open shift.

### 4. The receipt

When the sale is complete, the till app has the receipt, to print in its own layout on a receipt printer. With the **Receipts** feature on, it can also print the receipt as a page branded from your [Receipts settings](invoices-and-receipts.md), or email it to the buyer. Without Receipts, those two aren't available.

### 5. Cash in and out

Cash put in the drawer or taken out outside a sale, such as more change from the safe or a banking run, is recorded on the shift with its amount and a reason, such as *Banked at noon*.

### 6. Returns at the till

The staff member finds the sale, enters a reason such as *Wrong size*, and refunds all of it or part of it. Taking a return needs the **Refund at a register** permission, not just **Sell at a register**, and covers the sales of the register's own store; a sale of another store, or an online order, is returned only by staff who also have **Manage orders**.

- The items go back on the shelf at the register's store, unless staff choose not to restock them.
- **A return needs an open shift**: what the sale paid in cash is handed back from the drawer and recorded as cash taken out of the register's open shift. The site works out how much cash to hand back.
- **The card is refunded before the cash.** When a sale was paid partly in cash and partly by card, the card is refunded first, and only what it doesn't cover comes out of the drawer.
- The rest goes back to how it was paid: a Stripe reader's card is refunded through Stripe, and a gift card gets its money back. A card taken on the counter terminal is refunded on that terminal: the site records the refund, and staff make it on the terminal.
- **A till returns register sales only.** An online order can be returned at a till only by staff with **Manage orders**.

### 7. Close the shift

At the end of the shift, the staff member counts the cash in the drawer, enters it with a note if it's off, such as *Short 1.48, a coin dropped behind the till*, and closes the shift. Closing it gives the shift's sales, how they were paid, the cash the drawer should hold, the cash counted, and the difference.

While a shift is open, the till app can show the same at any time, so staff can see how the day is going.

## The cash drawer

<YouTube video="point-of-sale-till" title="Video of the register, the open shift and closing a shift" start={112} end={170} />

The drawer should hold:

- the **opening float**,
- plus the **cash kept** for sales: what buyers handed over, less the change given back,
- plus cash **put in**, less cash **taken out**, which includes cash handed back for returns.

That's the **expected** cash. At the close, the counted cash less the expected cash is the **difference**: negative when the drawer is short, positive when it's over.

## Shifts

<YouTube video="point-of-sale" title="Video of reading a shift" start={279} end={342} />

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
| **Tax collected** | The tax on the shift's sales. |
| **Opening float** | The cash put in the drawer at the start. |
| **Put in, less taken out** | The cash movements, returns at the till included. Each one is listed with its reason and who made it. |
| **Expected** | What the drawer should hold. |
| **Counted** and **Difference** | For a closed shift: the cash counted, and how far off it was. Negative is shown in red. |

**Departments** lists what each department rang up in the shift: how many, and what they came to before tax. The note given at the close is shown with it.

## What a register sale does differently

<YouTube video="point-of-sale-till" title="Video of register sales and returns as orders" start={170} end={199} />

A register sale is an order, like an online one, so it's under **Commerce > Orders**, in your reports, and in the buyer's account when you named a customer with an account. It differs in these ways:

- Its **channel** is *Point of sale*. The orders list filters by channel (**Every channel**, **Online**, **Point of sale**, **Drafts sent by staff**, **AI agents**), and its CSV export follows the filter. A register sale shows a badge with its register.
- Its **location** is the register's store: its stock is held and sold there. When that store has none of an item, the stock comes off your main location.
- It records the **register**, the **shift** and the **staff member** who rang it up.
- The buyer takes it with them: nothing is shipped, and its checkout skips the **Contact** and **Shipping** steps.
- It's **taxed** by the register's store address, and billed to it.
- It's paid in **cash**, by **card on the counter terminal**, by **card on a Stripe reader**, with a **gift card**, or a mix. Store credit and loyalty points aren't taken at a register.
- An amount rung up in a **department** is a line of the order with the department's name. It takes no stock, and it's listed with your products in the sales reports.

## For your developers

The till app is built on the [commerce API](headless-api.md). The [Point of Sale](../../modules/point-of-sale.md#api) page in the Technical Manual lists every request the till app sends, with a whole shift [call by call](../../modules/point-of-sale.md#a-shift-call-by-call).

## Related pages

- [Headless commerce API](headless-api.md)
- [Carts and orders](carts-and-orders.md)
- [Inventory](inventory.md)
- [Gift cards](gift-cards.md)
- [Invoices and receipts](invoices-and-receipts.md)
- [Payment providers](payment-providers.md)
