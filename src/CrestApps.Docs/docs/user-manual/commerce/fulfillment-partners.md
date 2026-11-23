---
sidebar_label: Fulfillment partners
title: Fulfillment partners
description: Connect your store to the outside companies that make and ship your products, such as a print-on-demand service, send paid orders to them from the order page or automatically, and follow what was sent.
technical_manual:
  - modules/fulfillment-partners
---

A **fulfillment partner** is an outside company that makes and ships your products for you. A print-on-demand service is one example: it prints a shirt when someone buys it and posts it to the buyer.

You connect your store to the partners you use, choose whether paid orders go to them by themselves, and follow what was sent on each order and on one list.

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Fulfillment Partners; Commerce > Fulfillment Requests |
| **Permission** | **Manage fulfillment partners and their credentials** for the settings page. **Ship orders and update their shipments** to send orders and see what was sent. |
| **Feature** | Fulfillment Partners, and one feature for each partner. Shipping is turned on with it. |

<AskYourAdmin />

Each partner is added to your site separately, so the partners you see depend on your site. Once a partner is connected and your products are linked to its products, your store can send paid orders to it for you. Tracking numbers don't come back to your store yet: the partner shows them in its own account, and you type them on the order's shipment.

## See which partners are connected

Open **Settings > Commerce > Fulfillment Partners**. The top of the page lists each partner that is turned on for your store, with **Connected** or **Not connected** beside it.

If the list is empty, no partner is turned on yet. Ask your administrator to turn on the partner you use.

## Connect a partner

Each partner adds its own section below the list, with the details it needs, usually an access key you create in your account with the partner. Follow the steps shown in that section, then click **Save**. Your store keeps keys encrypted and never shows them again.

Some partners also add a card to the product editor, where you choose which of the partner's products makes each of your products. A product that isn't linked is never sent to the partner. Link each product to one partner only: when two partners both make an item in an order, your store doesn't guess, and asks you instead.

## Send an order to a partner

1. Open the paid order under **Commerce > Orders**.
2. In the **Fulfillment partners** card, click **Send to fulfillment partners**.

Each partner receives only the items it makes that are still available to ship. Your store ships bundles itself. A bundle can share an order with items a partner ships. If only bundles remain, the card shows **Nothing to send** and you receive no fulfillment alert.

The card shows what each partner answered, why, and which items it was sent.

| Status | Meaning |
| --- | --- |
| **Sent** | The partner has the order, with its order number. |
| **Nothing to send** | Nothing left in the order is made by this partner. |
| **Waiting to be sent** | The partner was busy. Your store tries again in a few minutes. |
| **Needs attention** | The order was not sent, or it is not certain it arrived. The card says why. |

The items sent to a partner also appear in the order's **Shipments** card as a pending shipment noted "Sent to *partner*", so they aren't shipped twice. When the partner refuses the order, that shipment is removed and the items can be shipped another way.

When the card says it is not known whether the partner created the order, look for the order number in your account with the partner before you click **Send again**. Your store asks you to confirm, so the order isn't made twice.

Your store never tells a partner to start making an order. When the partner starts depends on the partner's own settings, for example whether you approve each order in your account with the partner first.

## Add the tracking number

When the partner ships the parcel, open the order, find the pending shipment noted "Sent to *partner*" in the **Shipments** card, and enter the carrier and tracking number from your account with the partner. The shipment is marked shipped, the order's fulfillment status is updated, and the buyer is told as for any other shipment.

## Follow every order sent to partners

Open **Commerce > Fulfillment Requests**. The list shows every order sent, or to be sent, to a partner, most recently changed first, with the partner, the status, why, and when it last changed.

- Search by order number, and filter by status from the list's toolbar, for example to see only **Needs attention**.
- Click the order number to open the order.
- Click **Send again** on a row that wasn't sent.

## Get notified when an order needs you

When the Notifications feature is on, everyone who may ship orders gets a notification when an order sent to a partner needs attention, with a link to the order. Each person can turn this notification type off in their notification preferences.

## Send paid orders automatically

Select **Send paid orders automatically** and click **Save**. Each paid order is then sent within a few minutes, and the order's **Fulfillment partners** card shows the result. Check **Commerce > Fulfillment Requests** for orders marked **Needs attention**.

When it is off, send each order from its page.
