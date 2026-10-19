---
sidebar_label: Inventory
title: Inventory
description: Count how many of each product and variant you have in each location, receive stock, record counts and corrections, move stock, take pre-orders, email shoppers when it's back, and order from suppliers.
technical_manual:
  - modules/inventory
---

**Inventory** counts how many units of each product, and each variant, you have on hand. Every change is recorded: stock that arrives, corrections, counts, and later what customers buy. You can always see why a number is what it is.

| | |
| --- | --- |
| **Menu** | Commerce > Inventory; Commerce > Purchase orders; Settings > Commerce > Inventory |
| **Permission** | **View inventory** to look, **Manage inventory** to change stock and the settings |
| **Feature** | Inventory; **Back-in-Stock Alerts** to email shoppers when a product is back; **Purchase Orders** to order stock from suppliers |

<AskYourAdmin />

## Start counting a product

Stock is counted for content types that have the **Inventory** part:

1. Under **Content > Content Definition > Content Types**, edit your product type and add the **Inventory** part.
2. Go to **Commerce > Inventory** and click **Find products to count**. Every product of that type, and each of its variants, gets a line with nothing on hand.

From then on, saving a product keeps its lines up to date: a new variant gets its own line, and a removed variant's line is archived with its history.

On the product's editor, the **Inventory** section sets how its stock is kept:

| Setting | What it does |
| --- | --- |
| **Count stock** | Untick it to stop counting the product. Its lines are archived. |
| **Allow backorders** | Keep selling when none are left. |
| **Low-stock threshold** | Stock counts as low at this many or fewer. Leave it empty to use the store's default. |
| **Ships from** | Optional, for a product not out yet: the date it ships. With **Allow backorders** ticked, buyers pre-order it while none are in stock. See [Sell before a product is out](#sell-before-a-product-is-out). |

Below the settings, **On hand** lists each variant with how many are available, and a **Change** button that opens it under **Commerce > Inventory**.

## Find stock

**Commerce > Inventory** lists every product and variant counted, with the fewest available first. Search by SKU or product name, and show **Low stock**, **Out of stock** or **Backordered** lines.

Each line shows:

- **On hand:** the units you hold;
- **Reserved:** units held for orders being paid for;
- **Available:** what can still be sold, which is on hand minus reserved.

## Change stock

Open a line to change it. Each change needs only a number and, if you like, a reason:

- **Received** records stock that arrived, such as a delivery. Enter how many came in.
- **Adjusted** records a correction up or down, such as damaged or found units. Enter a negative number to take stock away.
- **Count** records how many you counted. The count replaces what's on hand, and the history records the difference.

The history lists every change, newest first, with when it happened, what it was, the change, what was on hand after it, and who made it. **Recount from history** recomputes what's on hand from that history, for when a number looks wrong.

Two people changing the same line at once can't overwrite each other: the second is asked to try again.

## Stock in several locations

Keep stock in more than one place, such as a warehouse and a shop, under **Settings > Commerce > Inventory**, **Locations**:

1. Fill in the blank **Add a location** with its **Name**, such as *Main warehouse*, and, if you like, an **Address** for your staff.
2. Click **Save**. A new blank location is there for the next one.

The **Default location** is always there. It holds the stock you counted before there were locations, and stock imported without a location; rename it, but it can't be removed. Tick **Remove this location** to remove another: units still there stay on their items and can be moved to another location.

With more than one location, each item's page under **Commerce > Inventory**:

- lists its stock **By location**, with what's **On hand** and **Available** at each, and what's held for orders;
- asks where under **Receive or adjust** and **Count**, such as *At Main warehouse*: a count replaces what's on hand at that location;
- has **Move between locations**: choose **From** (with what it has available), **To**, how many and a note, and click **Move**. Only available units move, and the total on hand doesn't change. The history shows *Moved out* and *Moved in*, each with its location.

The list and the labels shoppers see count every location together.

**Which location an order takes from.** An order takes its units of each item from one location that has them all, trying the locations from the top of the list. An order picked up at one of your shops takes them from that shop's stock location first; see [Store pickup](shipping.md#store-pickup). Returned items go back where they were sold from, or where you choose when you receive them.

## Order stock from suppliers

With the **Purchase Orders** feature on, **Commerce > Purchase orders** keeps the stock you order from suppliers, and receives it into a location.

1. Click **Add purchase order**, and enter the **Supplier** (earlier suppliers are suggested), the **Supplier's email**, where to **Receive at**, what currency the costs are in (**Costs in**), when it's **Expected**, and a **Note**.
2. Leave **Start with the items that are low on stock where it's received** ticked to fill it with what's running low, each ordered up to twice its low-stock threshold, or untick it to start empty. Click **Start the order**.
3. Under **Add items**, click **Find items to order**, search by SKU or name, and click **Save**. Each comes in with a quantity of 1. Set how many are **Ordered** and the **Unit cost** of each, or tick **Remove**. The total adds up as you save.
4. When you've sent it to the supplier, click **Mark as ordered**. Its items can no longer change; the costs still can.

When stock arrives, enter what came under **Arrived now**, and click **Receive what arrived**, or **Receive everything** for all that's still to come. The units go on hand at the location, and each item's history shows *Received* with the purchase order's number. The order shows **Partly received** until everything is in, then **Received**.

**Cancel order** stops what's still to come; what was received stays in stock. A draft can be deleted. The list shows the open orders, newest first; search by number or supplier, and **Show** picks **Drafts**, **Ordered**, **Partly received**, **Received**, **Canceled** or **All**.

The supplier isn't emailed: send them the order yourself.

## Export and import counts

**Export CSV** downloads every product and variant counted, one per line, with its **Sku**, **Title**, **ContentItemId**, **VariantId**, **OnHand**, **Reserved**, **Available** and **LowStockThreshold**. It opens in any spreadsheet program.

To set many counts at once, such as after a stocktake:

1. Export the stock, and change the **OnHand** column to what you counted.
2. Click **Import CSV**, choose the file, and click **Import**.

Each line is found by its **ContentItemId** and **VariantId**, or by its **Sku** when those are missing or come from another site. Only **OnHand** is read; the other columns can stay or go. A line whose count differs is recorded as a **Count** with the reason *Imported from stock.csv.*, and a line that already matches is left alone.

If any line has a problem, such as a SKU nothing has, a SKU two variants share, the same item twice, or a count that isn't a whole number, nothing is imported. The first problems are listed by line, so you can fix the file and import it again. A file can have up to 10,000 lines.

## Low-stock alerts

When a product or variant drops to its low-stock threshold or sells out, users in the roles chosen under **Settings > Commerce > Inventory** get a notification, such as *Low stock: Mug* with *Only 4 of Mug left.*, or *Out of stock: Mug*. It's sent when the stock drops to the threshold, and again if it sells out, not on every sale in between. Notifications need Orchard Core's **Notifications** feature.

With **Workflows** enabled, the **Low Stock** event starts a workflow at the same moment, for example to email a supplier. Its input has the product as `ContentItem`, and `Sku`, `Title`, `Available`, `LowStockThreshold` and `SoldOut`.

## In-stock filter for listings

With **Product Listing** enabled, a listing can offer an **In stock** filter that shows only products some of which can be bought now. A product with variants is in stock when any variant is. A product on backorder isn't, and a product you don't count always is. Listings that offer every filter show it on their own; for a listing whose filters you chose, tick **In stock** under **Choose filters**. A product that sells out leaves the filtered list right away, and comes back when you receive more.

## What shoppers see

On product pages and listing cards, under the **Add to cart** button, shoppers see one of these:

| Label | When |
| --- | --- |
| **In stock** | More than the low-stock threshold is available. |
| **Only 3 left** | The threshold or fewer is available, with the number. |
| **Out of stock** | Nothing is available. **Add to cart** is turned off. |
| **Available on backorder** | Nothing is available, but the product allows backorders. |
| **Pre-order: ships** and the date | Nothing is available, the product allows backorders, and its **Ships from** date is still to come. The button reads **Pre-order**. |

Exact numbers above the threshold stay private. For a product with variants, the label follows the options the shopper picks, and the list of variants marks each one, such as *L / Matte — $29 (Out of stock)*.

The cart won't take more than is available. It says *Only 3 of Mug left.* when a shopper adds too many, and stops the checkout when a line has sold out since it was added. Units a shopper's own unpaid checkout holds still count as theirs, so coming back to the cart from checkout doesn't make their items look sold out.

## Sell before a product is out

To take orders for a product before it's released:

1. On the product's editor, under **Inventory**, tick **Allow backorders**.
2. Set **Ships from** to the day it ships.

While none are in stock and the date is still to come, the product shows *Pre-order: ships* with the date, and its button reads **Pre-order**. The cart, the mini cart, the order's page and your order page show the date on the line, as it was when the order was placed. After the date, the product is sold as an ordinary backorder; once you receive stock, it's sold from stock.

## Tell shoppers when it's back

With the **Back-in-Stock Alerts** feature on, the page of a sold-out product that doesn't allow backorders shows *Sold out. Get an email when it's back:* under **Add to cart**, with an email box and **Notify me**. For a product with variants, it shows while a sold-out variant is picked, and asks about that one. A signed-in shopper's email is filled in.

When the product or variant comes back in stock, such as when you receive more, everyone waiting is emailed *... is back in stock*, oldest request first, with a link to the product. Each request gets one email. The item's page under **Commerce > Inventory** says how many shoppers are waiting.

- The link to the product needs your site's **Base URL** under **Settings > General**; without it the email names the product only. Your site's email must be set up.
- One email address can ask about 10 products a day, and one visitor can send 20 requests an hour.
- A restock emails up to 500 shoppers; the rest are emailed when the item next comes back.
- A shopper's requests are part of their data: a customer's **Download data** includes them, and **Erase personal data** deletes them. See [Privacy requests](customers.md#privacy-requests).

## When customers buy

With **Orders** enabled, stock follows each order:

1. **Checkout starts:** the units the buyer is getting are held for them, so nobody else can buy them while they pay. The held units show as **Reserved**, and the item's page lists the orders holding stock and until when.
2. **Not enough left:** if a counted product doesn't have enough available units, and doesn't allow backorders, the buyer is sent back to the cart with a message such as *Only 1 of Mug left. Lower the quantity to check out.* or *Mug is out of stock. Remove it to check out.* Nothing is held for that order.
3. **Paid:** the held units are sold. They leave **On hand**, and the history shows **Sold** with the order.
4. **Not paid:** when the checkout is canceled, declined or abandoned, or the hold runs out, the units are released and can be bought again. A payment that arrives after the hold ran out still sells the units, even if that takes the count below zero.

5. **Refunded or canceled by the store:** when you tick **Put the items back in stock** on the order, what it sold goes back **On hand**, and the history shows **Returned** with the order.
6. **Returned by the buyer:** with the **Returns** feature, the items you tick when a return arrives go back **On hand**, at the location you choose or where they were sold from, and the history shows **Returned** with the return's number. See [Returns and exchanges](returns.md).

Two buyers can't both get the last unit: the second is told to try again.

## Settings

Under **Settings > Commerce > Inventory**:

| Setting | What it does | Default |
| --- | --- | --- |
| **Low-stock threshold** | Stock counts as low at this many available units or fewer, for products without their own. Changing it updates every product that uses the default. | 5 |
| **Hold stock for (minutes)** | How long units stay held for a buyer who started checkout and hasn't paid. | 60 |
| **Notify about low stock** | The roles whose users are told when a product runs low or sells out. | Administrator |
| **Locations** | The places you keep stock, in the order orders take units from them. See [Stock in several locations](#stock-in-several-locations). | The default location |

## Recipes and deployments

A recipe can set counts with a **Stock** step, finding each item by its ids or its SKU; see the [Inventory technical manual](../../modules/inventory.md) for its format.

To copy counts to another site, add the **Stock** step to a deployment plan (**Commerce** category). It exports every item with its ids and SKU. The products themselves need a content step, before the **Stock** step.

## Try a sample

The **Product listing: online store sample** recipe (**Configuration > Recipes**) counts every product it adds: most have 25, a few are running low, and the *Adventure Works Everyday Backpack* is sold out, so the shop shows each label.

## Related pages

- [Products and currencies](products-and-currencies.md)
- [Carts and orders](carts-and-orders.md)
- [Returns and exchanges](returns.md)
- [Shipping](shipping.md)
