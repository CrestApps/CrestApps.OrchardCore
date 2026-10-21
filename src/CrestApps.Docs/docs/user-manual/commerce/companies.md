---
sidebar_label: Companies
title: Companies
description: "Sell to businesses: companies with several buyers and admins, the places they ship to, payment terms with a deposit and an invoice due in net days, purchase order numbers, price lists, order limits and monthly budgets that need an admin's approval, and punchout from a company's procurement system."
technical_manual:
  - modules/companies
---

A **company** is a business you sell to. Several people buy for it, it may pay on account instead of at checkout, it can have its own prices, and its admins can approve the bigger orders its buyers place. A company buyer signs in as themselves and buys for the company.

| | |
| --- | --- |
| **Menu** | Commerce > Companies, Commerce > Price lists |
| **Permission** | Manage companies |
| **Feature** | Companies (it needs Orders) |

<AskYourAdmin />

## Add a company

1. On **Commerce > Companies**, click **Add company**.
2. Enter its **Name**, and if you like its **Tax or VAT number** (printed on its invoices) and a **Billing email** that gets its invoices too.
3. Under **Buyers**, enter each person's **User name or email** (they need a user account first), choose **Buyer** or **Admin**, and give a buyer an **Order limit** if their bigger orders need approval. An admin sees all of the company's orders and approves orders. Save to get more empty rows.
4. Fill in **Payment terms** and the other sections below, and click **Save**.

A user buys for one company. Turn **On** off to let a company's buyers buy for themselves again; its orders keep the company.

## Payment terms

| Field | What it does |
| --- | --- |
| **Net days** | Such as 30: what the company doesn't pay at checkout is invoiced, due in this many days. Leave it blank and the company pays at checkout like anyone else. |
| **Deposit (%)** | The part paid at checkout, such as 20. The rest goes on the invoice. |
| **Credit limit** | The most the company may owe at once. An order goes on account only as far as the limit allows; the rest is paid at checkout. |
| **Buyers give a purchase order number** | A buyer can't place an order without one. It's printed on the invoice. |

At checkout, a company buyer gets a **Company** step after shipping, where they enter the purchase order number. The payment step then offers to put the rest on the company's account. What the company owes shows on its **Company** page in the buyer's account, and in [Transactions](transactions.md).

## Locations

Under **Locations**, add the places the company has orders shipped to, with a name such as *Head office* and the address, in the same address form as the rest of the store: choose the country first and the form asks for what that country's addresses need. Its buyers pick one at checkout. To remove a location, clear its fields.

## Price lists

A **price list** gives the companies on it their own prices.

1. On **Commerce > Price lists**, click **Add price list**.
2. Name it, such as *Wholesale*. Only staff see the name.
3. Enter a **Percentage off everything else** if you like, such as 10.
4. Under **Prices by SKU**, enter a SKU and its price for each unit on each line, apart by a comma, such as *HAT-22069, 24.50*. Paste them from a spreadsheet if you have many. A variant's own SKU prices that variant.
5. Click **Save**, then choose the list under **Price list** on each company that buys at it.

A product on the list by SKU costs its price there; any other product costs your price less the percentage. Prices are in your store's currency: a buyer shopping in a [market](markets.md) with another currency pays them converted at its rate. A buyer always pays the lower of the list's price and yours, and volume or role discounts don't come off a list price too. Deleting a price list puts its companies back on your prices.

## Order limits, budgets and approvals

Two settings send a buyer's order to the company's admins before it can be paid:

- a buyer's **Order limit**: an order over it;
- the company's **Monthly budget**: an order that takes the company's paid orders this month past it.

An admin's own orders never need approval.

When an order needs approval, the buyer's **Company** step says why and offers **Ask for approval**. Once they ask, the company's admins are emailed, and the order waits under the buyer's orders as *Waiting for approval*; it can't be paid yet.

An admin approves or rejects it from the **Company** page of their account, where waiting orders are listed with who asked, why, and the items. They can add a note for the buyer. The buyer is emailed either way:

- **Approved**: the order shows *... approved this order. Pay it to place it.* with **Pay now**.
- **Rejected**: the order is canceled.

An order that grows past the total that was approved, such as with more items, needs approval again. The admin's **Company** page also shows the monthly budget and what's been spent.

## Quotes

Company buyers can ask for a quote from their cart, with their purchase order number, when your store takes quote requests. See [Quotes](carts-and-orders.md#quotes).

## Punchout

**Punchout** lets a company's procurement system, such as SAP, open your store: the buyer fills a cart, and it goes back to their system, where it's approved and ordered. Your store speaks OCI.

1. Add a user who only buys for the company (not a site administrator), and add them under **Buyers**.
2. Under **Punchout (OCI)**, tick **Its procurement system shops here by punchout**, and enter that user as the **Punchout buyer**.
3. Click **Save**. A message shows the company's new **punchout key** and its **punchout address**. Copy the key now: it isn't shown again.
4. Give the address and key to the company to set up in their procurement system. It sends the key as *PASSWORD* and the address to send carts back to as *HOOK_URL*.

The buyer then opens your store from their system, signed in as the punchout buyer, at the company's prices. On the cart, **Transfer to your procurement system** sends the items, quantities, prices and SKUs back.

To stop a key working, tick **Make a new key** and save, or turn punchout off.

## Related pages

- [Carts and orders](carts-and-orders.md): quotes and orders made for a buyer.
- [Checkout](checkout.md): the steps a buyer goes through.
- [Transactions](transactions.md): what a company owes on account.
- [Markets](markets.md): prices in other currencies.
