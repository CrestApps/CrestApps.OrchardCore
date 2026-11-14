---
sidebar_label: Invoicing
title: Invoicing
description: Create, issue, void and write off invoices, and set what new invoices start with.
technical_manual:
  - modules/invoicing
---

An **invoice** tells a customer what they owe you, line by line, with tax. While it is a **draft** you can change it freely. When you **issue** it, it gets its number, such as **INV-1001**, and what it says is owed is added to the customer's balances under **Commerce > Transactions**, where it is paid, reminded about and reported like any other balance. An issued invoice never changes: it is corrected with a credit note, voided, or written off.

| | |
| --- | --- |
| **Menu** | Commerce > Invoices; Settings > Commerce > Invoicing |
| **Permission** | View invoices; Create invoices and change drafts; Issue invoices; Void and write off invoices; Issue credit notes; Manage invoicing settings |
| **Feature** | Invoicing |

<AskYourAdmin />

## Find an invoice

Go to **Commerce > Invoices**. The newest invoices come first. Search by number, customer name or your reference, and narrow the list by **Status** (draft, open, written off, void) and **Payment** (unpaid, partly paid, paid). Click an invoice to open its page.

## Create an invoice

1. Go to **Commerce > Invoices** and click **New invoice**.
2. Under **Billed to**, click **Find a customer** and pick a user, customer or business account. To bill someone you only know by name, leave it empty and fill in **Name on the invoice** and **E-mail**.
3. Under **Lines**, fill in the empty row: pick its **Kind**, type what it is for, the **Quantity**, the **Price each** and, if you give one, a **Discount %**. A **Text** line prints a note and a **Section heading** a heading between lines; neither has an amount.
4. Click **Update**. The draft is saved, its totals are worked out, and another empty row appears for the next line. To remove a line, tick **Remove** and click **Update**.
5. Check **Payment** (currency, issue date, payment term, whether the customer may pay part of it, purchase order and reference) and **Printed text** (notes, terms, footer and an internal note only staff see). Fields left empty use the customer's details or the invoicing settings.
6. Click **Save draft** to keep it as a draft, or **Save and issue** to issue it now.

A draft has no number and owes nothing. Edit it as often as you like from its page with **Edit draft**, or delete it with **Delete draft**. Tax is worked out again when it is issued.

## Issue an invoice

On the draft's page, click **Issue** and confirm. You need **Issue invoices**.

## What happens when an invoice is issued

- It gets the next number of the invoice series. A draft that can't be issued (no customer, no currency, no line, or a line out of range) keeps no number.
- It keeps the **Issue date** you chose on the draft, or is dated today when you left it empty. It can't be dated after today unless **Allow issue dates after today** is on. Its due date comes from its payment term and that date.
- Your business details are copied onto it from the store profile, and the customer's details from their billing profile, so later changes to either never change it.
- What it says is owed is added to the customer's balances. An invoice that totals zero is paid at once.
- Its page shows what was paid and what is still due, and **Open the transaction** takes you to the balance under **Commerce > Transactions**, where payments are recorded. The balance's page names the invoice and links back to it.
- It can't be changed any more: **Edit** opens its page instead.

## Send, print and share an invoice

On the invoice's page, **Send and share** has:

- **Send**: opens the send dialog. Check **To** (the customer's billing e-mail), add **Copy to** and **Blind copy to** addresses if you like, separated by commas, and click **Send**. The invoice goes as a PDF attachment with a link to view it online and, while something is owed, a link to pay it. Sending a draft issues it first when **Sending a draft issues it** is on in the settings. E-mail must be set up for the site.
- **Print**: shows the invoice as the customer gets it, with **Print** and **Download PDF**.
- **PDF**: downloads the invoice. Once issued, it is always the PDF made when it was issued.
- **Share link**: click **Copy** and paste the link anywhere. Whoever opens it sees the invoice, can pay it and download it, without signing in. It stops working after the number of days set under **Share links last (days)**.

The card shows when the invoice was sent and when the customer first opened it. A credit note's page has **Print** and **PDF** too. To change the e-mail, edit **Invoice sent to its customer** under the commerce e-mails; to change how invoices look, edit the **Invoices** document template.

## What your customers see

A customer who signs in finds **Invoices** in their account. It lists the invoices billed to them and, for an admin of a business account, the account's invoices. They open an invoice, download it, and pay it; to pay several at once, they tick them and click **Pay selected**. Invoices owed by different customers, or in different currencies, are paid separately. They need the **View own invoices** permission.

On a business account's page, the **Invoices** section shows what the account owes and how much of it is overdue, its latest invoices, and **New invoice**, which starts an invoice billed to the account.

## Payments on an invoice

An invoice shows what was paid, what was credited and what is still due, and its history lists each payment and each payment taken back. Payments are recorded on the invoice's balance: click **Open the transaction** on the invoice's page, then use **Record an offline payment** or **Apply customer credit** there. The invoice follows at once; if it was busy at that moment, it catches up the next time you open it.

## Credit an invoice

Correct an issued invoice with a **credit note**, for example for goods returned or a price that was wrong. You need **Issue credit notes**.

1. On the invoice's page, under **Credit notes**, click **New credit note**. A draft is made with every line that can still be credited.
2. Type the **Reason**, printed on the credit note.
3. Pick how to **Give the credit**:
   - **Lower what is owed on the invoice**, while it is not paid in full. It can't lower the balance by more than is still owed.
   - **Refund what was paid**, through the payment provider the customer paid with.
   - **Keep it as the customer's credit**, for their next purchase or invoice (with the **Store Credit** feature).
4. To credit part of a line, lower its **Quantity** or **Price each**; to leave a line out, tick **Remove**. Click **Update** to see the total.
5. Click **Save and issue**, or **Save draft** to finish later.

The credit note gets its own number, such as **CN-1001**, and the invoice shows what it credited. An invoice can never be credited by more than its total, line by line. A draft credit note can be deleted; an issued one can't.

## Void, write off or reinstate an invoice

These are on the invoice's page, each with the reason you give.

- **Void** an invoice that was issued by mistake and on which nothing was paid. Its number is kept and nothing is owed any more. If a payment was recorded, or a credit note was issued for it, it can't be voided: refund the payment or issue a credit note instead.
- **Write off** what is still owed when you won't collect it. The invoice becomes **Uncollectible**.
- **Reinstate** a written-off invoice when the customer pays after all: what was written off is owed again.

You need **Void and write off invoices** for these, and you are asked for a reason each time.

## Choose what new invoices start with

1. Go to **Settings > Commerce > Invoicing**.
2. Fill in the fields below.
3. Click **Save**.

| Field | What it does |
| --- | --- |
| **Default payment term** | When an invoice is due, for customers who have no term of their own. **Due on issue** means it is due the day it is issued. Terms are kept under **Commerce > Payment terms**. |
| **Default currency** | The three-letter code, such as **USD**, of an invoice whose customer has no currency of their own. |
| **Prices include tax** | Line prices are taken as including tax, and the tax is worked out of them. |
| **Let customers pay part of an invoice** | Customers can pay any amount up to what is due from the invoice's pay link. |
| **Sending a draft issues it** | Sending a draft first issues it, so it gets its number. |
| **Allow issue dates after today** | An invoice can be issued with a date in the future. Off: it is dated the day it is issued or earlier. |
| **Date invoices in the order they are numbered** | An invoice can't be dated before the last invoice issued, so numbers and dates go up together, as some countries require. |
| **Cash rounding** | Rounds each total to this step, such as **0.05**, and prints the difference. Leave it empty for no rounding. |
| **Share links last (days)** | How long the link that shows an invoice without signing in keeps working. |
| **Default notes**, **Default terms**, **Default footer** | Printed on every invoice that has none of its own. |

## Automate invoices

With **Workflows** enabled, a workflow can start when an invoice is created, issued, sent, opened by the customer for the first time, receives a payment, is paid in full, becomes overdue or is voided, or when a credit note is issued for it. You find these events under **Invoicing** when you add a start to a workflow. A workflow can also create an invoice with one line, issue it, send it, record a payment received outside the site, void it, or credit it in full; each of these steps asks for the invoice, which in a workflow started by an invoice event is `{{ Workflow.Input.InvoiceId }}`.

For example, to thank customers when they pay, start a workflow with **Invoice Paid** and send an e-mail to `{{ Workflow.Input.BillToEmail }}`.

With **Commerce Webhooks** enabled, the same moments can be sent to another system: tick the events under **Invoicing** when you add a webhook under **Configuration > Webhooks** (see [Webhooks](webhooks.md)).

## Move invoices to another site

Under **Tools > Deployments > Plans**, add the **Invoices** and **Credit notes** steps to a plan to take every invoice and credit note to another site. Issued invoices keep their numbers there and don't change what customers owe: bring their balances with the transactions. An invoice the other site already issued is left as it is, and one whose number the other site already uses is not imported. New invoices on the other site skip the numbers that came in.

## Related pages

- [Transactions](transactions.md): the balances invoices post.
- [Payment Terms](payment-terms.md): when invoices are due.
- [Document Templates and Number Series](document-templates-and-number-series.md): how invoices look and how their numbers are written.
