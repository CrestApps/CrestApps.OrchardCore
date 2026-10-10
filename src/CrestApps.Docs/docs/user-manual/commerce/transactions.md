---
sidebar_label: Transactions
title: Transactions
description: See every payment your customers still owe, record payments taken outside the site, send reminders, and let customers pay their own balances from My Transactions.
technical_manual:
  - modules/transactions
  - modules/checkout
---

A **transaction** is a payment a customer owes you: a *Pay Later* order, a payment of an installment plan, or any other balance that was not paid in full at checkout. The **Transactions** report shows what is still owed across the whole site, and each customer sees their own balances on **My Transactions**, where they can pay them online.

## Transaction statuses

Every transaction shows its status as a colored badge.

| Status | What it means |
| --- | --- |
| **Pending** | Recorded but not yet due, for example a scheduled payment of an installment plan. |
| **Outstanding** | Owed in full and not paid yet. |
| **Partially paid** | Part of the balance is paid; the rest is still owed. |
| **Paid** | Paid in full. |
| **Canceled** | Canceled before it was paid. Nothing more is collected for it. |
| **Failed** | An attempt to collect the payment failed. |
| **Abandoned** | Left unpaid past the time it could be collected. |
| **Refunded** | Paid and later refunded. |

A transaction can still take a payment, be marked paid or be canceled while it is **Pending**, **Outstanding**, **Partially paid** or **Failed**. Once it is **Paid**, **Canceled**, **Abandoned** or **Refunded**, it is final.

## The Transactions report

The report lists every transaction on the site, newest first.

| | |
| --- | --- |
| **Menu** | Commerce > Transactions |
| **Permission** | Manage transactions |
| **Feature** | Transactions |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of filtering the Transactions report, opening a transaction, recording an offline payment and the reminder settings">
  <source src="/img/docs/um-transactions.mp4" type="video/mp4" />
</video>

### Find a transaction

1. Open **Commerce > Transactions**.
2. Type part of the transaction's title in **Search by title** and press Enter.
3. To narrow the list, pick a status in the **Any status** list in the list header, or a source in the **Any source** list. The list updates as soon as you pick.

The status list offers **Outstanding** as well as each status: it shows everything that still has a balance, which is both **Outstanding** and **Partially paid** transactions. The source list shows where transactions come from on your site, for example **Pay Later** or **Installment plan**, depending on which features are turned on.

The header shows how many transactions are on this page and how many match in total. Use the pager under the list to move between pages, and **Items per page** to show more or fewer rows.

### What each row shows

| Badge | What it shows |
| --- | --- |
| Status | The transaction's status. |
| **Owner** | The customer who owes the money. *Unknown* means the customer's account could not be found. |
| **Source** | Where the transaction came from, such as **Pay Later** or **Installment plan**. |
| **Total** | The full amount of the transaction, with its currency. |
| **Outstanding** | The amount still owed. Shown only while there is a balance on a transaction that is still open. |
| **Due** | The date the payment is due, when it has one. |

Hover over a badge to see its name. Click the title or **Manage** to open the transaction.

## Manage a transaction

The transaction's page has a **Summary** card and a **Timeline** on the left, and the actions on the right.

### Summary

| Field | What it shows |
| --- | --- |
| **Owner** | The customer who owes the money. |
| **Source** | Where the transaction came from. |
| **Reference** | What the transaction is for, such as the order or plan it belongs to. |
| **Total** | The full amount. |
| **Paid** | How much has been paid so far. |
| **Outstanding** | How much is still owed. |
| **Created** | When the transaction was recorded. |
| **Due** | When the payment is due, if it has a due date. |
| **Invoice** | The invoice number, such as *INV-1001*, once the customer has been sent an invoice. With the **Payment Receipts** feature on, it opens the [printable invoice](invoices-and-receipts.md#print-an-invoice). |
| **Settled** | When the transaction was settled, and whether it was paid *online* or *offline*. |
| **Reminders sent** | How many payment reminders the customer has been sent. |

### Timeline

The **Timeline** lists everything that happened to the transaction, newest first: when it was created, each payment, each reminder, status changes, cancellations and notes. Each entry shows who did it, when a person did. A payment entry has a **View receipt** link when the **Payment Receipts** feature is on; see [Invoices and Receipts](invoices-and-receipts.md).

### Record a payment taken outside the site

Use this when the customer paid by cash, check or bank transfer.

1. Open the transaction from **Commerce > Transactions**.
2. In **Record an offline payment**, check the **Amount**. It starts at the full outstanding balance; change it for a part payment.
3. Optionally, describe how the payment was received in **Note**.
4. Click **Record payment**, then click **Record payment** again in the confirmation box.

If the payment covers the balance, the transaction becomes **Paid**. If it covers part of it, the transaction becomes **Partially paid**. An amount larger than the balance is recorded as the balance, so the customer never owes a negative amount. With the **Payment Receipts** feature on, the customer is sent a receipt.

:::note[The customer sees the note]
The note you type on a recorded payment becomes part of the payment's entry in the customer's history. Write it for the customer, for example *Paid by bank transfer*. For something only staff should read, use **Add a note** instead.
:::

### Mark a transaction as paid

Use this to settle the whole balance at once, for example when the customer paid the full amount another way.

1. Open the transaction.
2. Click **Mark as paid**, then click **Mark as paid** in the confirmation box.

The remaining balance is recorded as an offline payment and the transaction becomes **Paid**. With the **Payment Receipts** feature on, the customer is sent a receipt for that amount.

### Cancel a transaction

Cancel a transaction when nothing more should be collected for it.

1. Open the transaction.
2. Optionally, type a **Cancellation reason (optional)**.
3. Click **Cancel transaction**, then click **Cancel transaction** in the confirmation box. Click **Keep it** to back out.

The transaction becomes **Canceled**, and no more reminders are sent for it. The reason is shown in the customer's history too. A transaction that is already **Paid**, **Abandoned** or **Refunded** can't be canceled: if money was collected, [refund the payment](payments-and-refunds.md#refund-a-payment) instead.

### Send a reminder now

The **Send a reminder** card appears when the **Transaction Reminders** feature is on.

1. Open the transaction.
2. In **Send a reminder**, click **Send reminder**.

The reminder goes out at once, with no confirmation, and counts toward **Reminders sent**. The button is unavailable when nothing is owed. If you see *The reminder could not be sent. The owner may not have a notification channel configured.*, the customer has no way to receive notifications; contact them another way.

### Add an internal note

1. Open the transaction.
2. Type in **Add a note** and click **Add note**.

Notes appear on the **Timeline** for staff. Customers never see them.

:::tip[Someone else changed it]
If two people work on the same transaction at once, the second to save sees *This transaction was changed by someone else while you were working on it. Reload the page and try again.* Reload the page, check what changed, and repeat your action if it is still needed.
:::

## Payment reminders

With the **Transaction Reminders** feature on, the site reminds customers about money they owe. It checks about every six hours.

- **Before the due date:** once per transaction, a set number of days before it falls due. If the payment is charged automatically to a saved card, the reminder says which card will be charged and on which date, and tells the customer they don't need to do anything. If the customer pays it themselves, the reminder is their invoice, with a link that pays it.
- **On and after the due date:** for each transaction that is **Outstanding** or **Partially paid** and past its due date, a first reminder, then more at a fixed interval, up to a maximum number. A transaction with no due date is counted from the day it was created.

Each reminder states the amount still owed and what it is for. The first one on the due date says the payment is *due today*; later ones say the balance is outstanding. A reminder for a payment the customer pays themselves also carries the **invoice number** and a **link that pays it without signing in**; see [Invoices and Receipts](invoices-and-receipts.md).

Customers with an account get reminders through the site's notifications, on the methods they chose. Guests who bought without an account get them by email, at the address they gave when they bought. Every reminder sent is added to the transaction's **Timeline**.

### Change when reminders are sent

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Transactions |
| **Permission** | Manage transaction settings |
| **Feature** | Transaction Reminders |

<AskYourAdmin />

1. Open **Settings > Commerce > Transactions**.
2. Change the fields below and click **Save**.

| Field | What it does |
| --- | --- |
| **Enable scheduled reminders** | Sends reminders automatically on the schedule below. Clear it to send reminders only by hand, with **Send reminder**. On by default. |
| **First reminder delay (days)** | How many days after the due date the first reminder is sent. 0, the default, sends it on the due date. |
| **Reminder interval (days)** | How many days to wait between reminders for the same transaction. At least 1; the default is 7. |
| **Maximum reminders** | The most reminders sent for one transaction. 0 means no limit; the default is 3. |
| **Remind before the due date (days)** | How many days before the due date the customer is told a payment is coming due. Sent once per payment. 0 turns these reminders off; the default is 3. |

A reminder you send by hand counts toward **Maximum reminders**, and the next scheduled reminder waits a full interval after it.

## My Transactions (for customers)

Customers see and pay their own balances on **My Transactions**. Every signed-in user has the **View own transactions** permission by default.

| | |
| --- | --- |
| **Menu** | Commerce > My Transactions |
| **Permission** | View own transactions |
| **Feature** | Transactions |

<AskYourAdmin />

### See what you owe

1. Open **Commerce > My Transactions**.
2. To see only what you still owe, pick **Outstanding** in the **Any status** list in the list header.
3. To find one transaction, type part of its description in **Search by description** and press Enter.

Each row shows the status, the **Total**, the **Outstanding** amount when something is still owed, and the **Due** date when there is one.

### Open a transaction

Click the transaction's title or **View**. The page shows:

- **Details:** the **Total**, how much is **Paid**, what is **Outstanding**, when it was **Created**, the **Due** date and the **Invoice** number, once you have been sent an invoice. Click the invoice number to see and print the invoice.
- **History:** each payment, reminder and change, newest first. A payment has a **View receipt** link to its printable receipt.

### Pay online

1. Click **Pay now**, either on the row in **My Transactions** or on the transaction's page.
2. The site opens checkout with the outstanding balance ready to pay. Finish the payment there; see [Checkout](checkout.md).

When the payment is confirmed, the transaction becomes **Paid**, or **Partially paid** if less than the balance was collected, and you are sent a receipt. If you see *Online settlement is not available*, online payment isn't set up on this site; contact the site's administrator to pay another way.

Customers can also pay from the link in a reminder, without signing in; see [Pay from a reminder](invoices-and-receipts.md#pay-from-a-reminder).

## Related pages

- [Invoices and Receipts](invoices-and-receipts.md): invoice numbers, pay links, printable invoices and receipts.
- [Payments and Refunds](payments-and-refunds.md): every payment taken at checkout, and refunding one.
- [Installment plans](installment-plans.md): payment schedules whose payments appear here as transactions.
