---
sidebar_label: Document Templates and Number Series
title: Document Templates and Number Series
description: How to give receipts, invoices and other business documents your logo, colors, paper size and footer, and how to choose how their numbers are written.
technical_manual:
  - modules/business-documents
---

Business documents are the papers your business sends: receipts, payment requests, invoices, estimates and credit notes. A **document template** gives them your look: a logo, an accent color, a paper size and a footer. **Number series** decide how their numbers are written, such as **INV-2026-0001**.

| | |
| --- | --- |
| **Menu** | Settings > Business Documents > Document Templates, Settings > Business Documents > Number Series |
| **Permission** | Manage business document templates (templates), Manage number series (number series) |
| **Feature** | Business Documents, and Business Documents PDF for PDF files |

<AskYourAdmin />

## Add a document template

1. Go to **Settings > Business Documents > Document Templates**.
2. Click **Add template**.
3. Fill in the fields below.
4. Click **Save**.

The list shows each template with the kinds of documents it is for and whether it is the default. You can show the list as rows or as a grid. Click a template's name or **Edit** to change it, and **Delete** to remove it.

### The template

| Field | What it does |
| --- | --- |
| **Name** | A name staff recognize, such as Standard or Wholesale. Required. |
| **Notes** | Notes for staff. They are not printed. |
| **Used for** | The kinds of documents the template is for, such as Receipts or Payment requests (the invoice of a balance a customer pays themselves, with the Transactions and Receipts features on). Tick none to use it for every kind. Only the kinds of the features you have enabled are listed. |
| **Default template** | Documents of its kinds that name no template are printed with it. When you save a template as the default, the template that was the default for the same kinds stops being the default. |

### Look

| Field | What it does |
| --- | --- |
| **Logo** | The address of a PNG or JPEG image in the media library, such as /media/logo.png. PDF files show only an image from the media library. |
| **Use an accent color** | Tick it to print the title, the header of the line table and the total in the accent color. |
| **Accent color** | The accent color. |
| **Paper size** | **Letter** or **A4**, for PDF files and printed pages. **The renderer's default (Letter)** leaves the choice to the document, and prints on Letter when it has none. |
| **Footer** | Text printed at the bottom of the documents, such as a registration number or a thank-you note. |

A document that has its own logo, accent color, paper size or footer keeps it. The template only fills what the document leaves empty.

:::tip[Which template is used]
A document that names a template is printed with it when the template is for the document's kind. Otherwise it is printed with the default template for its kind. A default made for the kind wins over a default for every kind. When no template fits, the document is printed with its own look only.
:::

## Choose how document numbers are written

1. Go to **Settings > Business Documents > Number Series**. Every number series of the features you have enabled is listed, such as receipts and invoices.
2. For a series, change the fields below. The **Example** changes as you type.
3. Click **Save**.

| Field | What it does |
| --- | --- |
| **Pattern** | How the numbers are written: text with tokens in braces. The hint under the field shows the pattern the series started with. |
| **First number** | Where a new series starts, and where it starts again each year or month. Numbers already issued do not change. |
| **Start again** | **Never**, **Each year** or **Each month**: when the numbers start again from the first number. |
| **Example** | The first number of the series, written with today's date. |

The tokens are:

| Token | Is written as |
| --- | --- |
| `{seq}` | The number, such as 42. |
| `{seq:0000}` | The number with at least as many digits as zeros, such as 0042. |
| `{yyyy}` | The year, such as 2026. |
| `{yy}` | The year in two digits, such as 26. |
| `{MM}` | The month in two digits, such as 05. |

The dates are the day the number is issued, in your site's time zone.

### Rules for a pattern

- A pattern has exactly one `{seq}` token, so that each number is different.
- Numbers that start again each year need `{yyyy}` or `{yy}`, and numbers that start again each month need `{MM}` too, or numbers would repeat.
- A pattern has at most 40 characters, and the first number is 1 or more.

When a series breaks a rule, the page shows the message next to it and saves nothing until you correct it. A series that you set back to the pattern, first number and restart it started with goes back to following the feature that registered it, and its **Customized** badge goes away.

## Related pages

- [Invoices and Receipts](invoices-and-receipts.md): the receipts and invoices that use these templates and numbers.
