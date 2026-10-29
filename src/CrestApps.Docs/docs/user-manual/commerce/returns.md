---
sidebar_label: Returns
title: Returns and exchanges
description: Let buyers ask to send items back for a refund or an exchange, approve or decline each request, put what comes back into stock, and refund or close it.
technical_manual:
  - modules/carts-and-orders
---

**Returns** lets buyers ask to send items of a paid order back, from their order's page, for their money back or for something else, such as another size. You approve or decline each request, mark the items received, put what can be sold again back into stock, then refund the buyer or send the replacement. Each step is emailed to the buyer and written in the order's notes.

| | |
| --- | --- |
| **Menu** | Commerce > Returns; Settings > Commerce > Returns |
| **Permission** | **Manage orders** to handle returns and change the settings. Buyers need nothing more than their order: their account, or the order link a guest has. |
| **Feature** | Returns |

<AskYourAdmin />

## Set up returns

<YouTube video="orders" title="Video of the returns list and the return settings" start={267} end={292} />

Under **Settings > Commerce > Returns**:

| Setting | What it does | Default |
| --- | --- | --- |
| **Return window (days)** | For how many days after paying a buyer can ask to return items. After that, the **Return items** button no longer shows. From 1 to 365. | 30 |
| **Reasons** | One per line. Buyers choose one for each item they return. | *Doesn't fit*, *Not as described*, *Arrived damaged*, *Changed my mind*, *Other* |
| **Let buyers ask for something else instead of a refund** | Offers an exchange, such as another size. You send the replacement and close the return. | On |
| **Return instructions** | What the buyer is told once you approve a return, such as where to send the items. Emailed to them and shown on their order. | None |

## What buyers do

A paid order with goods shows a **Returns** box on its page, in the buyer's account or on the order link a guest has, with **Return items** while the return window is open.

1. The buyer clicks **Return items**, chooses **How many** of each item and **Why**.
2. With exchanges on, they choose **My money back** or **Something else instead, such as another size**, and say **What would you like instead?**
3. They can add a comment, then click **Ask to return**.

They're emailed that the request arrived, and the **Returns** box lists it as **Requested**, with **Withdraw this request** until you answer. Once you approve it, the box shows **How to send it back**: your return instructions, your message, and the return number to put in the parcel.

Only goods can be returned: downloads and services have nothing to send back. An item can't be returned more times than it was bought, counting earlier returns that weren't declined or withdrawn.

## Handle a return

**Commerce > Returns** lists the open returns, newest first. Search by return or order number, or show **Requested**, **Approved**, **Received**, **Completed**, **Declined**, **Withdrawn** or **All**. A return's page shows what's coming back, why, what it was paid, what the buyer wants and what they wrote, with a link to the order. The order's page lists its returns too.

| Status | Meaning |
| --- | --- |
| **Requested** | The buyer asked; you haven't answered. |
| **Approved** | You agreed, and the buyer was told how to send the items back. |
| **Received** | The items arrived back. |
| **Completed** | Refunded, or the replacement was sent. |
| **Declined** | You said no, with why. |
| **Withdrawn** | The buyer took the request back before you answered. |

### Approve or decline

- Under **Approve**, add a **Message for the buyer (optional)** and click **Approve**. They're emailed your return instructions, your message and the return number.
- Under **Decline**, say **Why (the buyer is told)** and click **Decline the return**. You can decline a return until it's received.

### The items arrived

Under **The items arrived**, tick what can be sold again, and click **Mark received**. The ticked items go back into stock; the others don't. When you keep stock in more than one place, choose **Back into stock at**, or leave **Where it was sold from**. See [Stock in several locations](inventory.md#stock-in-several-locations).

You can mark a return received without approving it first, such as when a parcel arrives unannounced.

### Refund

For a buyer who wants their money back, **Refund** shows the **Amount**, which starts at what the items were paid, before tax and shipping, and can be up to what can still be refunded on the order. Click **Refund and close**. The money goes back the way the buyer paid, the order shows the refund, and the buyer is emailed. You can refund before the items arrive, such as for a damaged item you don't want back.

### Exchange

For a buyer who wants something else, send them what they asked for, then under **Exchange** add a message if you like, such as the tracking number, and click **The replacement is on its way**. The return is completed and the buyer is emailed. The replacement isn't made into an order for you: send it as you would any parcel, or [create an order](carts-and-orders.md#create-an-order-for-a-buyer) for it.

## Things to know

- The return window runs from when the order was paid.
- Returns start with the buyer; you can't open one for them. Refunds, and putting everything an order sold back into stock, stay on the order's page: see [Refund an order](carts-and-orders.md#refund-an-order).
- Emails need your site's email to be set up; without it, returns work and nobody is emailed.

## Related pages

- [Carts and orders](carts-and-orders.md)
- [Inventory](inventory.md)
- [Payments and refunds](payments-and-refunds.md)
