---
sidebar_label: Invoicing
title: Invoicing
description: Set what new invoices start with, and what issuing, voiding and writing off an invoice does.
technical_manual:
  - modules/invoicing
---

An **invoice** tells a customer what they owe you, line by line, with tax. While it is a **draft** you can change it freely. When you **issue** it, it gets its number, such as **INV-1001**, and what it says is owed is added to the customer's balances under **Commerce > Transactions**, where it is paid, reminded about and reported like any other balance. An issued invoice never changes: it is corrected with a credit note, voided, or written off.

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Invoicing |
| **Permission** | Manage invoicing settings |
| **Feature** | Invoicing |

<AskYourAdmin />

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

## What happens when an invoice is issued

- It gets the next number of the invoice series. A draft that can't be issued (no customer, no currency, no line, or a line out of range) keeps no number.
- It is dated today, and its due date comes from its payment term.
- Your business details are copied onto it from the store profile, and the customer's details from their billing profile, so later changes to either never change it.
- What it says is owed is added to the customer's balances. An invoice that totals zero is paid at once.

## Void, write off or reinstate an invoice

- **Void** an invoice that was issued by mistake and on which nothing was paid. Its number is kept and nothing is owed any more. If a payment was recorded, or a credit note was issued for it, it can't be voided: refund the payment or issue a credit note instead.
- **Write off** what is still owed when you won't collect it. The invoice becomes **Uncollectible**.
- **Reinstate** a written-off invoice when the customer pays after all: what was written off is owed again.

You need **Void and write off invoices** for these, and you are asked for a reason each time.

## Related pages

- [Transactions](transactions.md): the balances invoices post.
- [Payment Terms](payment-terms.md): when invoices are due.
- [Document Templates and Number Series](document-templates-and-number-series.md): how invoices look and how their numbers are written.
