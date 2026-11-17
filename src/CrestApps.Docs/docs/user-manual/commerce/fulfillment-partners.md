---
sidebar_label: Fulfillment partners
title: Fulfillment partners
description: Connect your store to the outside companies that make and ship your products, such as a print-on-demand service, and send paid orders to them from the order page or automatically.
technical_manual:
  - modules/fulfillment-partners
---

A **fulfillment partner** is an outside company that makes and ships your products for you. A print-on-demand service is one example: it prints a shirt when someone buys it and posts it to the buyer.

On this page you connect your store to the partners you use, and choose whether paid orders go to them by themselves.

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Fulfillment partners |
| **Permission** | **Manage fulfillment partners and their credentials** |
| **Feature** | Fulfillment Partners, and one feature for each partner |

<AskYourAdmin />

Each partner is added to your site separately, so the partners you see depend on your site. Once a partner is connected and your products are linked to its products, your store can send paid orders to it for you. Tracking numbers don't come back to your store yet: the partner shows them in its own account.

## See which partners are connected

Open **Settings > Commerce > Fulfillment partners**. The top of the page lists each partner that is turned on for your store, with **Connected** or **Not connected** beside it.

If the list is empty, no partner is turned on yet. Ask your administrator to turn on the partner you use.

## Connect a partner

Each partner adds its own section below the list, with the details it needs, usually an access key you create in your account with the partner. Follow the steps shown in that section, then click **Save**. Your store keeps keys encrypted and never shows them again.

Some partners also add a card to the product editor, where you choose which of the partner's products makes each of your products. A product that isn't linked is never sent to the partner.

## Send an order to a partner

1. Open the paid order under **Commerce > Orders**.
2. In the **Fulfillment partners** card, click **Send to fulfillment partners**.

The card then shows what each partner answered:

| Status | Meaning |
| --- | --- |
| **Sent** | The partner has the order, with its order number. |
| **Nothing to send** | Nothing in the order is made by this partner. |
| **Waiting to be sent** | The partner was busy. Your store tries again in a few minutes. |
| **Needs attention** | The order was not sent, or it is not certain it arrived. The card says why. |

When the card says it is **not known** whether the partner created the order, look for the order number in your account with the partner before you click **Send again**, so the order isn't made twice.

Your store never tells a partner to start making an order. When the partner starts depends on the partner's own settings, for example whether you approve each order in your account with the partner first.

## Send paid orders automatically

Select **Send paid orders automatically** and click **Save**. Each paid order is then sent within a few minutes, and the order's **Fulfillment partners** card shows the result. Check the card of orders marked **Needs attention**.

When it is off, send each order from its page.
