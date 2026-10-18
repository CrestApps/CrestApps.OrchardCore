---
sidebar_label: Inventory
title: Inventory
description: Count how many of each product and variant you have, receive stock, record counts and corrections, and see every change.
technical_manual:
  - modules/inventory
---

**Inventory** counts how many units of each product, and each variant, you have on hand. Every change is recorded: stock that arrives, corrections, counts, and later what customers buy. You can always see why a number is what it is.

| | |
| --- | --- |
| **Menu** | Commerce > Inventory; Settings > Commerce > Inventory |
| **Permission** | **View inventory** to look, **Manage inventory** to change stock and the settings |
| **Feature** | Inventory |

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

Exact numbers above the threshold stay private. For a product with variants, the label follows the options the shopper picks, and the list of variants marks each one, such as *L / Matte — $29 (Out of stock)*.

The cart won't take more than is available. It says *Only 3 of Mug left.* when a shopper adds too many, and stops the checkout when a line has sold out since it was added. Units a shopper's own unpaid checkout holds still count as theirs, so coming back to the cart from checkout doesn't make their items look sold out.

## When customers buy

With **Orders** enabled, stock follows each order:

1. **Checkout starts:** the units the buyer is getting are held for them, so nobody else can buy them while they pay. The held units show as **Reserved**, and the item's page lists the orders holding stock and until when.
2. **Not enough left:** if a counted product doesn't have enough available units, and doesn't allow backorders, the buyer is sent back to the cart with a message such as *Only 1 of Mug left. Lower the quantity to check out.* or *Mug is out of stock. Remove it to check out.* Nothing is held for that order.
3. **Paid:** the held units are sold. They leave **On hand**, and the history shows **Sold** with the order.
4. **Not paid:** when the checkout is canceled, declined or abandoned, or the hold runs out, the units are released and can be bought again. A payment that arrives after the hold ran out still sells the units, even if that takes the count below zero.

5. **Refunded or canceled by the store:** when you tick **Put the items back in stock** on the order, what it sold goes back **On hand**, and the history shows **Returned** with the order.

Two buyers can't both get the last unit: the second is told to try again.

## Settings

Under **Settings > Commerce > Inventory**:

| Setting | What it does | Default |
| --- | --- | --- |
| **Low-stock threshold** | Stock counts as low at this many available units or fewer, for products without their own. Changing it updates every product that uses the default. | 5 |
| **Hold stock for (minutes)** | How long units stay held for a buyer who started checkout and hasn't paid. | 60 |
| **Notify about low stock** | The roles whose users are told when a product runs low or sells out. | Administrator |

## Recipes and deployments

A recipe can set counts with a **Stock** step, finding each item by its ids or its SKU; see the [Inventory technical manual](../../modules/inventory.md) for its format.

To copy counts to another site, add the **Stock** step to a deployment plan (**Commerce** category). It exports every item with its ids and SKU. The products themselves need a content step, before the **Stock** step.

## Try a sample

The **Product listing: online store sample** recipe (**Configuration > Recipes**) counts every product it adds: most have 25, a few are running low, and the *Adventure Works Everyday Backpack* is sold out, so the shop shows each label.

## Related pages

- [Products and currencies](products-and-currencies.md)
- [Carts and orders](carts-and-orders.md)
