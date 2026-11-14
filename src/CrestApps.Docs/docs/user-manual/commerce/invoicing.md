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
| **Permission** | View invoices; Create invoices and change drafts; Issue invoices; Void and write off invoices; Manage invoicing settings |
| **Feature** | Invoicing |

<AskYourAdmin />

## Find an invoice

Go to **Commerce > Invoices**. The newest invoices come first. Search by number, customer name or your reference, and narrow the list by **Status** (draft, open, written off, void) and **Payment** (unpaid, partly paid, paid). Click an invoice to open its page.

## Create an invoice

1. Go to **Commerce > Invoices** and click **New invoice**.
2. Under **Billed to**, click **Find a customer** and pick a user, customer or business account. To bill someone you only know by name, leave it empty and fill in **Name on the invoice** and **E-mail**.
3. Under **Lines**, fill in the empty row: pick its **Kind**, type what it is for, the **Quantity**, the **Price each** and, if you give one, a **Discount %**. A **Text** line prints a note and a **Section heading** a heading between lines; neither has an amount.
4. Click **Update**. The draft is saved, its totals are worked out, and another empty row appears for the next line. To remove a line, tick **Remove** and click **Update**.
5. Check **Payment** (currency, payment term, whether the customer may pay part of it, purchase order and reference) and **Printed text** (notes, terms, footer and an internal note only staff see). Fields left empty use the customer's details or the invoicing settings.
6. Click **Save draft** to keep it as a draft, or **Save and issue** to issue it now.

A draft has no number and owes nothing. Edit it as often as you like from its page with **Edit draft**, or delete it with **Delete draft**. Tax is worked out again when it is issued.

## Issue an invoice

On the draft's page, click **Issue** and confirm. You need **Issue invoices**.

## What happens when an invoice is issued

- It gets the next number of the invoice series. A draft that can't be issued (no customer, no currency, no line, or a line out of range) keeps no number.
- It is dated today, and its due date comes from its payment term.
- Your business details are copied onto it from the store profile, and the customer's details from their billing profile, so later changes to either never change it.
- What it says is owed is added to the customer's balances. An invoice that totals zero is paid at once.
- Its page shows what was paid and what is still due, and **Open the transaction** takes you to the balance under **Commerce > Transactions**, where payments are recorded. The balance's page names the invoice and links back to it.
- It can't be changed any more: **Edit** opens its page instead.

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
| **Cash rounding** | Rounds each total to this step, such as **0.05**, and prints the difference. Leave it empty for no rounding. |
| **Share links last (days)** | How long the link that shows an invoice without signing in keeps working. |
| **Default notes**, **Default terms**, **Default footer** | Printed on every invoice that has none of its own. |

## Related pages

- [Transactions](transactions.md): the balances invoices post.
- [Payment Terms](payment-terms.md): when invoices are due.
- [Document Templates and Number Series](document-templates-and-number-series.md): how invoices look and how their numbers are written.
