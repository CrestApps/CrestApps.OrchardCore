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

<YouTube video="commerce" title="Narrated video of the Transactions report, a transaction, and the reminder settings" start={408} end={454} />

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

<YouTube video="payments" title="Video of a transaction: reminders, the invoice, adjusting a balance and recording a payment" start={172} end={241} />

The transaction's page has a **Summary** card and a **Timeline** on the left, and the actions on the right.

### Summary

| Field | What it shows |
| --- | --- |
| **Owner** | The customer who owes the money. |
| **Source** | Where the transaction came from. |
| **Reference** | What the transaction is for, such as the order or plan it belongs to. |
| **Total** | The full amount. |
| **Adjusted** | How much was taken off what is owed without being paid, such as a discount or a waiver. Shown once anything has been. |
| **Paid** | How much has been paid so far. |
| **Refunded** | How much has been given back, once anything has. |
| **Outstanding** | How much is still owed: the total, less what was adjusted and what was paid. |
| **Created** | When the transaction was recorded. |
| **Due** | When the payment is due, if it has a due date. |
| **Invoice** | The invoice number, such as *INV-1001*, once the customer has been sent an invoice. With the **Receipts** feature on, it opens the [printable invoice](invoices-and-receipts.md#print-an-invoice). |
| **Settled** | When the transaction was settled, and how: *Paid online*, *Recorded by hand*, or *Nothing left to pay after an adjustment*. |
| **Reminders sent** | How many payment reminders the customer has been sent. |

### Timeline

The **Timeline** lists everything that happened to the transaction, newest first. Each entry is headed **Created**, **Payment received**, **Reminder sent**, **Invoice sent**, **Adjusted**, **Status changed**, **Canceled**, **Refunded** or **Note**, and shows who did it, when a person did. A payment entry has a **View receipt** link when the **Receipts** feature is on; see [Invoices and Receipts](invoices-and-receipts.md).

### Record a payment taken outside the site

Use this when the customer paid by cash, check or bank transfer.

1. Open the transaction from **Commerce > Transactions**.
2. In **Record an offline payment**, check the **Amount**. It starts at the full outstanding balance; change it for a part payment.
3. Optionally, type the check number or transfer reference in **Reference**. It is recorded with the payment.
4. Optionally, describe how the payment was received in **Note**.
5. Click **Record payment**, then click **Record payment** again in the confirmation box.

If the payment covers the balance, the transaction becomes **Paid**. If it covers part of it, the transaction becomes **Partially paid**. An amount larger than the balance is recorded as the balance, so the customer never owes a negative amount. With the **Send Payment Receipts** feature on, the customer is sent a receipt.

:::note[The customer sees the note]
The note you type on a recorded payment becomes part of the payment's entry in the customer's history. Write it for the customer, for example *Paid by bank transfer*. For something only staff should read, use **Add a note** instead.
:::

### Mark a transaction as paid

Use this to settle the whole balance at once, for example when the customer paid the full amount another way.

**Record payment**, **Mark as paid** and **Cancel transaction** are only available while the transaction can still be collected: **Pending**, **Outstanding**, **Partially paid** or **Failed**. On a **Paid**, **Canceled**, **Abandoned** or **Refunded** transaction they are greyed out.

1. Open the transaction.
2. Click **Mark as paid**, then click **Mark as paid** in the confirmation box.

The remaining balance is recorded as an offline payment and the transaction becomes **Paid**. What was adjusted is not counted as paid. With the **Send Payment Receipts** feature on, the customer is sent a receipt for that amount.

### Cancel a transaction

Cancel a transaction when nothing more should be collected for it.

1. Open the transaction.
2. Optionally, type a **Cancellation reason (optional)**.
3. Click **Cancel transaction**, then click **Cancel transaction** in the confirmation box. Click **Keep it** to back out.

The transaction becomes **Canceled**, and no more reminders are sent for it. The reason is shown in the customer's history too. A transaction that is already **Paid**, **Abandoned** or **Refunded** can't be canceled: if money was collected, [refund the payment](payments-and-refunds.md#refund-a-payment) instead.

### Adjust or waive what is owed

Use this to lower what the customer owes without recording money you did not receive: financial aid, a discount given after the fact, a partial waiver, a credit the customer had, or an amount you write off.

1. Open the transaction.
2. In **Adjust or waive**, pick the **Type**: **Discount**, **Waiver**, **Credit** or **Write-off**.
3. Check the **Amount**. It starts at the full outstanding balance; change it to take off only part. It can't be more than what is still owed.
4. Optionally, type a **Reason**. It is printed on the invoice.
5. Click **Adjust**, then click **Adjust** in the confirmation box.

The amount appears as **Adjusted** in the summary and as an **Adjusted** entry on the timeline, and the customer owes that much less. The invoice lists each adjustment as a line that takes its amount off, and receipts say how much was taken off. If nothing is left to pay, the transaction becomes **Paid** and no more reminders are sent for it. An adjustment is not money received, so the customer is not sent a receipt for it. **Adjust** is only available while the transaction can still be collected and something is owed.

### Send the invoice now

The **Send the invoice** card appears when the **Transaction Reminders** feature is on, for a transaction the customer pays themselves (not one charged automatically to a saved card).

1. Open the transaction.
2. In **Send the invoice**, click **Send invoice**, then click **Send invoice** in the confirmation box, which names the amount still owed.

The transaction gets its invoice number, such as *INV-1001*, if it has none yet, and the customer is sent a notice titled *Invoice INV-1001* with a link that pays it. It is not worded as a reminder. It counts as the first notice: it adds to **Reminders sent**, and the next scheduled reminder waits a full interval after it. If you see *The invoice could not be sent. The owner may not have a notification channel configured.*, contact the customer another way; the invoice number is kept for the next try.

### Send a reminder now

The **Send a reminder** card appears when the **Transaction Reminders** feature is on.

1. Open the transaction.
2. In **Send a reminder**, click **Send reminder**, then click **Send reminder** in the confirmation box, which names the amount still owed.

The reminder goes out at once and counts toward **Reminders sent**. The button is unavailable when nothing is owed or the transaction can no longer be collected. If you see *The reminder could not be sent. The owner may not have a notification channel configured.*, the customer has no way to receive notifications; contact them another way.

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

- **Details:** the **Total**, how much was **Adjusted** (taken off without being paid, when anything was), how much is **Paid**, what is **Outstanding**, when it was **Created**, the **Due** date and the **Invoice** number, once you have been sent an invoice. Click the invoice number to see and print the invoice.
- **History:** each payment, invoice, reminder, adjustment, refund and change, newest first. A payment has a **View receipt** link to its printable receipt.

### Pay online

1. Click **Pay now**, either on the row in **My Transactions** or on the transaction's page. It is offered only while something is owed and the transaction can still be paid, so not on one that is **Canceled**, **Abandoned** or **Refunded**.
2. The site opens checkout with the outstanding balance ready to pay. Finish the payment there; see [Checkout](checkout.md). You can enter a promotion code on the **Payment** step: what it takes off is recorded on the transaction as a discount, and a 100% code pays the whole balance.

When the payment is confirmed, the transaction becomes **Paid**, or **Partially paid** if less than the balance was collected, and you are sent a receipt. If you see *Online settlement is not available*, online payment isn't set up on this site; contact the site's administrator to pay another way.

Customers can also pay from the link in a reminder, without signing in; see [Pay from a reminder](invoices-and-receipts.md#pay-from-a-reminder).

### Pay in installments

When the site lets you pay a balance in installments, checkout opens on a **Choose how to pay** step before the payment. It asks *How would you like to pay* the balance and lists:

- **Pay in full**: the whole balance today, as usual.
- One or more installment choices, such as **3 monthly payments**, each saying what is charged today and what follows, for example *USD 100.00 today, then 2 more payments of USD 100.00, charged to your card automatically.*

To pay in installments:

1. Pick the installment choice you want and click **Continue**.
2. The step now says *You chose 3 monthly payments.* and the order summary shows only the first payment.
3. On the **Payment** step, enter your card. The page says *The card you pay with now is saved, and the remaining payments are charged to it automatically on their due dates.* Only payment methods that can save a card are offered.
4. Click **Pay now**.

When the first payment is confirmed, your payment plan starts. The original balance is canceled because the plan replaces it, and each payment of the plan appears in **My Transactions** with its due date. On each due date the saved card is charged without you having to do anything. If a charge is declined, you are told, and the card is tried again; see [When a card is declined](installment-plans.md#when-a-card-is-declined).

If you leave checkout before paying the first payment, nothing changes: you still owe the original balance, and you can pay it in full or choose installments again later.

Installments are offered only for a balance that is still owed in full and belongs to a customer account (also when you pay from a reminder link), and only when the site has a payment method that can save a card. Which balances can be split, and into how many payments, is set by the site; see [Let customers choose installments at checkout](installment-plans.md#let-customers-choose-installments-at-checkout).

## My Transactions on the website

Customers who never open the admin can see their transactions on the website itself, in your site's own design. They see the same transactions, invoices and receipts as on **Commerce > My Transactions**, and only their own.

| | |
| --- | --- |
| **Page** | Your site's address followed by */my-transactions*, for example *https://shop.contoso.com/my-transactions* |
| **Permission** | View own transactions |
| **Feature** | Transactions, and Receipts for invoices and receipts |

<AskYourAdmin />

The page asks the customer to sign in first. A guest who bought without an account has no page here; they pay and see their invoice from the link in a reminder instead.

### See your transactions on the website

1. Open *my-transactions* on the site, or follow a link the site sent you.
2. To find one transaction, type part of its description in **Search by description**. To see only what you still owe, tick **Only what I owe**. Click **Filter**.

Each transaction shows its status, the **Total**, the **Outstanding** amount while something is owed, and the **Due** date when there is one. Under it are links to its invoice, such as **Invoice INV-1001**, once you have been sent one, and to the receipt of each payment, such as **Receipt R-1001**. **Pay now** appears while something is owed; it opens the transaction's [pay page](invoices-and-receipts.md#the-pay-page).

### Print an invoice or a receipt on the website

1. Click the invoice or receipt link under the transaction.
2. Click **Print invoice** or **Print receipt**. Only the document is printed.

While the invoice is still due, its page also has **Pay now**. **Back to my transactions** returns to the list.

Your site's designer can change how these pages look, or show the same documents inside the site's own customer area.

## Related pages

- [Invoices and Receipts](invoices-and-receipts.md): invoice numbers, pay links, printable invoices and receipts.
- [Payments and Refunds](payments-and-refunds.md): every payment taken at checkout, and refunding one.
- [Installment plans](installment-plans.md): payment schedules whose payments appear here as transactions.
