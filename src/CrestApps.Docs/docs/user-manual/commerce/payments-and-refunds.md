---
sidebar_label: Payments and Refunds
title: Payments and Refunds
description: Review every payment taken at checkout, see why a payment failed, give money back with a full or partial refund, and settle refunds that have to be paid back by hand.
technical_manual:
  - modules/payments
  - modules/checkout
  - modules/transactions
---

The **Payments** list is a record of every payment the site has tried to take at checkout, whether it went through or not. From it you can **refund** a payment, in full or in part. The **Refunds** list shows every refund and lets you close the ones that had to be paid back outside the site.

Refunding is kept apart from managing transactions on purpose: giving money back can't be undone, so it has its own permission.

| | |
| --- | --- |
| **Menu** | Commerce > Payments, Commerce > Refunds |
| **Permission** | Manage payments and refunds |
| **Feature** | Transactions, together with Checkout. The two menu items appear only when Checkout is on. |

<AskYourAdmin />

## Review payments

1. Open **Commerce > Payments**. The newest payments are at the top.
2. To narrow the list, pick a state in the **Any state** list in the list header, or a payment method in the **Any method** list. The list updates as soon as you pick.

The header shows how many payments are on this page and how many match in total. Use the pager under the list to move between pages, and **Items per page** to show more or fewer rows.

### Payment states

| State | What it means |
| --- | --- |
| **Created** | The payment was recorded, and the payment provider has not been asked yet. |
| **Pending** | The provider was asked, and the result is not known yet. |
| **Succeeded** | The provider confirmed the payment. Shown in green. |
| **Failed** | The provider declined or could not take the payment. Shown in red. |
| **Canceled** | The payment was canceled before it went through. |

### What each row shows

The row's title is the amount, tax included. For a payment that went through, it is the amount the provider confirmed; otherwise it is the amount the site asked for. When the payment paid a [transaction](transactions.md), the amount links to it.

| Badge | What it shows |
| --- | --- |
| State | The payment's state. |
| **Method** | The payment method used, by its short name. |
| **Updated** | When the payment last changed. |
| **Tax included** | The tax in the amount, when there was any. |
| **Gateway reference** | The payment provider's own reference for the payment. Use it to find the same payment in the provider's dashboard. |

When a payment **Failed**, the reason the provider gave is shown in red under the row, for example a declined card. Hover over a badge to see its name.

The buttons on the right:

- **Transaction** opens the transaction the payment paid, when it paid one.
- **Refund** opens the refund form. It appears only on a payment that **Succeeded**.

## Refund a payment

1. Open **Commerce > Payments** and find the payment. Picking **Succeeded** in the **Any state** list helps.
2. Click **Refund**. The **Refund payment** page shows the **Method**, the provider's **Payment** reference, how much was **Collected**, and how much is **Already refunded**.
3. Check **Amount to refund**. It starts at everything that can still be refunded. Lower it for a partial refund.
4. Type a **Reason**. It is kept with the refund, so anyone reading the list later knows why the money went back.
5. Click **Refund**, then click **Refund** again in the **Refund the customer** confirmation box. Click **Cancel** to back out.

| Field | What it does |
| --- | --- |
| **Amount to refund** | How much to give back, in the payment's currency. It must be more than zero and no more than what is left to refund. Tax is refunded in the same proportion, using the tax that applied when the payment was taken. |
| **Reason** | Why the money is going back. Shown under the refund in the **Refunds** list. |

You can refund one payment several times, for example half now and the rest later, until the whole amount is refunded. A refund that is still being processed already counts toward **Already refunded**, so the same money can't be refunded twice.

After you confirm, the site tells you what happened:

| Message | What it means |
| --- | --- |
| *The refund was completed.* | The provider sent the money back. |
| *The refund failed:* followed by a reason | The provider refused the refund. Nothing was sent back. |
| *This payment method cannot refund automatically. The refund was recorded for you to settle by hand.* | Pay the customer back yourself, then [mark the refund settled](#settle-a-refund-by-hand). |
| *The refund was submitted and is still being processed.* | The provider hasn't confirmed it yet. Check the **Refunds** list later. |

## What the customer is told

When a refund of a payment on a transaction succeeds, and the **Payment Receipts** feature is on, the customer is sent a notice titled *Your refund of*, followed by the amount. It says the money went back to the card or account they paid with and can take a few business days to appear on their statement. Customers with an account get it through the site's notifications; guests get it by email. The transaction's **Timeline** notes that the customer was told. See [Invoices and Receipts](invoices-and-receipts.md#refund-notices).

A refund doesn't change the transaction's status or balance.

## Review refunds

1. Open **Commerce > Refunds**. The newest refunds are at the top.
2. To narrow the list, pick a status in the **Any status** list in the list header, or a payment method in the **Any method** list.

The row's title is the amount refunded. Its badge shows the status:

| Badge | What it means |
| --- | --- |
| **Refunded** | The money went back to the customer. In the status list, this is **Succeeded**. |
| **Needs manual settlement** | The site couldn't send the money back itself. In the status list, this is **PendingManualReview**. |
| **Requested** | The refund was recorded, and the provider has not been asked yet. |
| **Pending** | The provider was asked, and has not confirmed it yet. |
| **Failed** | The provider refused the refund. |
| **Canceled** | The refund was canceled before it was sent. |

The row also shows the **Method**, when it was **Updated**, and the **Tax refunded**. Under the row is the reason you typed, in grey, or the reason it failed, in red.

## Settle a refund by hand

A refund **Needs manual settlement** when the site can't send the money back itself, for example for a payment method with no online refunds, such as Pay Later, or when the payment collected tax and the site can't work out how much of it to give back. Pay the customer back yourself, by bank transfer, check or through your payment provider's dashboard, then record it:

1. Open **Commerce > Refunds**. Picking **PendingManualReview** in the **Any status** list helps.
2. Optionally, type your own reference for the repayment, such as a bank transfer number, in **Reference** on the refund's row.
3. Click **Mark settled**, then click **Mark settled** in the confirmation box.

The refund becomes **Refunded**, and the customer is told as described above. Only a refund that **Needs manual settlement** can be marked settled, and it can't be undone.

## Related pages

- [Transactions](transactions.md): balances customers still owe, and recording payments taken outside the site.
- [Payment providers](payment-providers.md): the payment methods customers can use.
- [Checkout](checkout.md): how customers pay.
