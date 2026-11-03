---
sidebar_label: B2B Accounts
title: B2B Accounts
description: "Sell to businesses: let an account buy as a business, with several buyers and admins, the places it ships to, payment terms with a deposit and an invoice due in net days, purchase order numbers, price lists, order limits and monthly budgets that need an admin's approval, and punchout from its procurement system."
technical_manual:
  - modules/accounts-b2b
---

A business you sell to is an [account](../accounts.md), the same account your sales team keeps its contacts and deals in. Turn on **Business buying** for an account and several people can buy for it. It can pay on account instead of at checkout, have its own prices, and its admins can approve the bigger orders its buyers place. A buyer signs in as themselves and buys for the account.

| | |
| --- | --- |
| **Menu** | Commerce > B2B Accounts, Commerce > Price lists |
| **Permission** | Manage B2B accounts |
| **Feature** | Accounts B2B (it needs Accounts and Orders) |

<AskYourAdmin />

## Let an account buy as a business

1. Open the account, or create one, and click **Edit**.
2. Under **Business buying**, switch on **This account buys as a business**. If you like, enter its **Tax or VAT number** (printed on its invoices) and a **Billing email** that gets its invoices too.
3. Under **Members**, enter each person who buys for it by their **User name or email** (they need a user account first), and choose **Member** or **Admin**. An admin sees all of the account's orders and approves orders. See [the people who sign in for an account](../accounts.md#the-people-who-sign-in-for-an-account).
4. Fill in **Payment terms** and the other sections below, and click **Publish**.
5. Once the members are saved, give a buyer an **Order limit** under **Buyers** if their bigger orders need approval.

**Commerce > B2B Accounts** lists the accounts that buy as a business. A user is a member of one account, and buys for it. Switch **This account buys as a business** off to let its buyers buy for themselves again; its orders keep the account.

When a buyer's email is the email of one of the account's contacts, the buyer is linked to that contact, and the account's page shows a **Contact** link next to them.

## The account's page

Open the account with **View**. With business buying on, its page has a **Business buying** section with its payment terms, what it owes and its buyers, an **Invoices** section with the invoices for what it bought on its terms, and an **Orders** section with its orders. See [the account's page](../accounts.md#the-accounts-page).

## Payment terms

| Field | What it does |
| --- | --- |
| **Net days** | Such as 30: what the account doesn't pay at checkout is invoiced, due in this many days. Leave it blank and the account pays at checkout like anyone else. |
| **Deposit (%)** | The part paid at checkout, such as 20. The rest goes on the invoice. |
| **Credit limit** | The most the account may owe at once. An order goes on account only as far as the limit allows; the rest is paid at checkout. |
| **Buyers give a purchase order number** | A buyer can't place an order without one. It's printed on the invoice. |

At checkout, a buyer gets a **Business account** step after shipping, where they enter the purchase order number. The payment step then has a **Pay on account** box: *Pay a 20% deposit now, and $316 on Northwind Traders's terms, due in 30 days.* Click **Pay on account**, and the order shows what is on account and what is **Due now**; pay that as usual.

When the account pays its invoice, open it under **Commerce > Transactions** and enter the amount under **Record an offline payment**.

The invoice is the transaction *on account*, with its purchase order number, due in net days; the deposit is its own transaction. What the account owes shows on the account's page, on the **Business account** page in the buyer's own account, and in [Transactions](transactions.md).

## Locations

The account's **Locations** are the places it has orders shipped to; its buyers pick one at checkout. See [an account's locations](../accounts.md#an-accounts-locations).

## Price lists

A **price list** gives the accounts on it their own prices.

1. On **Commerce > Price lists**, click **Add price list**.
2. Name it, such as *Wholesale*. Only staff see the name.
3. Enter a **Percentage off everything else** if you like, such as 10.
4. Under **Prices by SKU**, enter a SKU and its price for each unit on each line, apart by a comma, such as *HAT-22069, 24.50*. Paste them from a spreadsheet if you have many. A variant's own SKU prices that variant.
5. Click **Save**, then choose the list under **Price list** on each account that buys at it.

A product on the list by SKU costs its price there; any other product costs your price less the percentage. Prices are in your store's currency: a buyer shopping in a [market](markets.md) with another currency pays them converted at its rate. A buyer always pays the lower of the list's price and yours, and volume or role discounts don't come off a list price too. Deleting a price list puts its accounts back on your prices.

## Order limits, budgets and approvals

Two settings send a buyer's order to the account's admins before it can be paid:

- a buyer's **Order limit**: an order over it;
- the account's **Monthly budget**: an order that takes the account's paid orders this month past it.

An admin's own orders never need approval.

When an order needs approval, the buyer's **Business account** step says why and has an **Ask for approval** box to tick. Once they ask, the account's admins are emailed, and the order waits under the buyer's orders as *Waiting for approval*; it can't be paid yet.

An admin approves or rejects it from the **Business account** page of their own account, where waiting orders are listed with who asked, why, and the items. They can add a note for the buyer. The buyer is emailed either way:

- **Approved**: the order shows *... approved this order. Pay it to place it.*, with the admin's note, and **Pay now**. **Pay now** opens the checkout at the **Business account** step again, where the buyer enters the purchase order number once more.
- **Rejected**: the order is canceled.

An order that grows past the total that was approved, such as with more items, needs approval again. The admin's **Business account** page also shows the monthly budget and what's been spent.

## Quotes

Buyers can ask for a quote from their cart, with their purchase order number, when your store takes quote requests. See [Quotes](carts-and-orders.md#quotes).

## Punchout

**Punchout** lets an account's procurement system, such as SAP, open your store: the buyer fills a cart, and it goes back to their system, where it's approved and ordered. Your store speaks OCI.

1. Add a user who only buys for the account (not a site administrator), add them under **Members**, and save.
2. Under **Punchout (OCI)**, tick **Its procurement system shops here by punchout**, and choose that member as the **Punchout buyer**.
3. Click **Publish**. A message shows the account's new **punchout key** and its **punchout address**. Copy the key now: it isn't shown again.
4. Give the address and key to the business to set up in their procurement system. It sends the key as *PASSWORD* and the address to send carts back to as *HOOK_URL*.

The buyer then opens your store from their system, signed in as the punchout buyer, at the account's prices. On the cart, **Transfer to your procurement system** sends the items, quantities, prices and SKUs back.

To stop a key working, tick **Make a new key** and publish, or turn punchout off.

## Moving price lists to another site

Price lists can be exported with the **Price Lists** deployment step and imported with a recipe. Accounts, with their business settings, move with the ordinary content export.

## Related pages

- [Accounts](../accounts.md): the businesses you work with, and their contacts.
- [Carts and orders](carts-and-orders.md): quotes and orders made for a buyer.
- [Checkout](checkout.md): the steps a buyer goes through.
- [Transactions](transactions.md): what an account owes on its terms.
- [Markets](markets.md): prices in other currencies.
