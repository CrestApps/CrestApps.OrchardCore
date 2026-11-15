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

## Ask for a deposit first

To be paid part of a job before you start:

1. Create the invoice for the whole job and click **Save draft**.
2. On its page, under **Deposits**, enter a percent (or an amount) and click **Ask for a deposit first**. A **deposit invoice** is drafted for that part; issue and send it.
3. When the customer has paid the deposit, open the draft again: the deposit is listed under **Deposits**. Click **Deduct from this invoice**; a line takes what was paid off the total.
4. Issue the invoice when the work is done.

An estimate that asked for a deposit works the same way: the deposit invoice made from it is deducted automatically when you invoice the whole estimate. Customers can also pay an invoice in installments when the store offers installments for invoices; see [Installment plans](installment-plans.md).

## Charge an invoice to the customer's saved card

Tick **Charge the customer's saved card** in the invoice editor, or choose **Issues the invoice and charges the customer's saved payment method** on a recurring plan. When the invoice is issued, it is charged to the customer's default saved card: at once when it is due, or on the morning of its due date. A declined card is tried again on the retry schedule set under **Settings > Commerce > Transactions**. When the customer has no saved card (customers billed by name and e-mail only never do), or every try is declined, the invoice is e-mailed to them to pay instead. The invoice's transaction shows the scheduled charge; see [Transactions](transactions.md#a-balance-charged-to-a-saved-card).

## Estimates

With **Invoicing Estimates** enabled, **Commerce > Estimates** lists the quotes you send before the work.

1. Click **New estimate**, choose who it is for, add the lines, and set **Valid until**, a **Deposit** if you ask one, and **When accepted** (do nothing, make the invoice, or make a deposit invoice).
2. Click **Save and send**. The estimate gets its number, such as **EST-1001**, and can no longer be changed. With e-mail set up, **Send to the customer** e-mails it with its PDF; otherwise copy the **Link for the customer** from its page.
3. The customer opens the link, reads the estimate, and types their name to **Accept** it, or **Decline** it. If they answer by phone or e-mail, use **Mark accepted** or **Mark declined** on its page.
4. **Make the invoice** puts the estimate on a draft invoice; **Invoice the deposit** makes a deposit invoice first. Check the draft and issue it.

To change a sent estimate, click **Revise**: you get a copy to change, sent as **EST-1001-2**, and the old one is canceled. An estimate nobody answered expires the day after **Valid until**. How long estimates are valid by default is set under **Settings > Commerce > Invoicing**.

## Late fees

With **Invoicing Collections** enabled, **Commerce > Late fees** holds the policies that charge customers who pay late. Each late fee is a separate **debit note**, numbered like an invoice and linked to the overdue invoice; the invoice itself never changes.

1. Click **Add late-fee policy** and give it a name.
2. Choose whether **Each fee is** a fixed amount or a percent of what is still due, and enter it.
3. Set the **Grace days** after the due date before the first fee. For a monthly fee, set **Repeat every (days)** to 30, and optionally **Most fees** and a **Cap** on the total.
4. Optionally, set a **Minimum balance** below which no fee is charged, tick **Tax the fee** if late fees are taxed where you are, and tick **E-mail each fee to the customer**.
5. Click **Save**.

Fees are charged automatically every hour. An overdue invoice's page lists its late fees and has **Charge the late fee due now**; a debit note's page links to the invoice it charges a fee on. To spare one invoice, tick **No late fees** in its editor. A late fee can be voided or credited like any invoice.

## Recurring invoices

With **Invoicing Recurring** enabled, **Commerce > Recurring invoices** lists the plans that bill a customer on a schedule, such as monthly hosting or a yearly support contract.

1. Click **New recurring plan** and give it a name.
2. Under **Schedule**, choose how often it repeats (every so many weeks, months or years, counted from **Starts**), and, if it should stop, **Ends** or **Number of invoices**. A **Custom schedule** takes a recurrence rule, such as `FREQ=MONTHLY;BYMONTHDAY=-1` for the last day of each month.
3. Choose what **Each run** does: make a draft for you to check, issue the invoice, issue and e-mail it, or issue it and charge the customer's saved payment method.
4. Choose who it bills and add the lines. Write `{{ period.month }} {{ period.year }}` in a line, such as `Hosting {{ period.month }} {{ period.year }}`, to print the month each invoice bills; `{{ period.start }}` and `{{ period.end }}` print the first and last day.
5. Optionally, set a **Price rise**, such as 5% every 12 runs.
6. Click **Save**, then **Start the plan** on its page.

The plan's page shows its next runs and every run it made, with a link to each invoice. Runs happen on their own; **Run what is due now** makes the invoices due today at once. A run that can't issue its invoice (for example, with no one to bill) keeps the draft and shows as **Failed** with the reason, and the plan carries on with its next run. A plan that starts in the past catches up the runs it missed, a year at most at a time.

**Pause** stops the runs; **Resume from today** carries on, without invoicing what was missed while paused. **Cancel the plan** stops it for good; the invoices it made stay as they are. A change to a running plan applies to the runs after it.

## See how invoicing is doing

With **Reports** enabled, **Reports > Commerce** has six invoicing reports. Each shows amounts per currency.

- **Receivables aging**: what customers still owe today and how late it is, from not due yet to over 90 days.
- **Invoice register**: every invoice issued in a period, with what was paid and what is still due.
- **Payments collected**: the payments received toward invoices in a period, by method, and the refunds.
- **Invoiced sales by customer** and **Invoiced sales by item**: what was invoiced in a period, less credit notes.
- **Invoiced tax summary**: the tax invoiced per tax and rate, less what credit notes gave back.

Pick the period at the top of a report; it starts on the last 30 days. To build your own report or export, use the **Invoicing** data source in the Report Builder or a data pipeline.

## Privacy requests

When you download or erase a customer's data under **Commerce > Customers**, their invoices and credit notes are included: those billed to them, to their account on the site, or to their e-mail. Invoices billed to a business account stay the business's. Erasing removes the customer's name, e-mail, phone and addresses from their invoices, but keeps the invoice numbers, amounts, dates and tax ids and the PDFs that were issued, because a business must keep its invoices. The invoice then shows **No name kept** as who it was billed to.

## Move invoices to another site

Under **Tools > Deployments > Plans**, add the **Invoices** and **Credit notes** steps to a plan to take every invoice and credit note to another site. Leave **Also export the transactions the invoices posted** ticked on the Invoices step, so what customers owe comes along and open invoices can be paid on the other site. Issued invoices keep their numbers there. An open invoice that arrives without its transaction says **Payments are not available on this site** on its page, and customers see no way to pay it until the transaction is imported. An invoice the other site already issued is left as it is, and one whose number the other site already uses is not imported. New invoices on the other site skip the numbers that came in.

## Related pages

- [Transactions](transactions.md): the balances invoices post.
- [Payment Terms](payment-terms.md): when invoices are due.
- [Document Templates and Number Series](document-templates-and-number-series.md): how invoices look and how their numbers are written.
