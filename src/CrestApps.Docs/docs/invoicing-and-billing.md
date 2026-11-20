---
sidebar_label: Overview
title: Invoicing & Billing
description: An overview of the invoicing and billing modules (Invoicing, the Transactions ledger, receipts, business documents and number series, exchange rates, subscriptions and installment plans), the features they turn on, and how a debt flows from a document to a payment.
user_manual:
  - user-manual/invoicing-and-billing
---

The invoicing and billing modules bill customers and collect what they owe. They run on their own, without the store: Invoicing depends on Transactions, Payment Terms, Taxation, Business Documents and **Commerce Foundation**, not on the **Commerce** store layer, so a site that only bills has the Commerce menu and the customer account without the store's setup guide. See [The Commerce Foundation and Commerce features](modules/commerce#the-commerce-foundation-and-commerce-features).

For the admin screens, see the User Manual's [Invoicing & Billing](user-manual/invoicing-and-billing.md) section.

## Modules

| Module | What it owns | Page |
| --- | --- | --- |
| **Invoicing** (`CrestApps.OrchardCore.Invoicing`, with Estimates, Billing, Collections, Recurring and API features) | Invoices, credit and debit notes, estimates, deposits and payment schedules, approvals and revisions, billing charges, time and usage, late fees, statements, recurring plans, the invoices of orders and of deferred debts, the API, AI tools, workflows, webhooks and reports | [Invoicing](modules/invoicing) |
| **Transactions** (with Payment Terms, Transaction Reminders and Send Payment Receipts) | The ledger of every amount owed and paid, payment terms, staged reminders, saved-card charging with retries, customer credit and the customer's statement | [Transactions](modules/transactions) |
| **Receipts** | Receipt documents and their templates | [Receipts](modules/receipts) |
| **Business Documents** (with PDF) | Document templates, number series and the PDF renderer every financial document uses | [Business Documents](modules/business-documents) |
| **Exchange Rates** | Daily rates and the reporting currency invoices record their issue day's rate in | [Exchange Rates](modules/exchange-rates) |
| **Subscriptions** | Recurring plans, the subscription checkout and subscribers | [Subscriptions](modules/subscriptions) |
| **Installment plans** | Payment plans charged to a saved card or invoiced on each due date | [Installment plans](modules/installment-plans) |

Taxes come from [Taxation](modules/taxation) (one rounding setting for checkout and invoices), and [Pay Later](modules/pay-later) lets a checkout leave a debt that Invoicing invoices, due under the payer's payment term.

## How a debt flows

1. **A debt is written on the ledger.** Issuing an invoice posts a transaction for it. An order paid on account or with Pay Later, a subscription's Pay Later checkout and each renewal write their own debt, and Invoicing issues an invoice that takes it over rather than adding a second one ([Deferred debts](modules/invoicing#deferred-debts-renewals-and-other-checkouts)).
2. **The due date follows payment terms.** The invoice's term, the payer's billing-profile term or the default term; Pay Later uses the payer's term, then its own.
3. **The customer is told and reminded.** The invoice is e-mailed with its PDF; Transaction Reminders chase the debt on its schedule, naming the invoice's number.
4. **Payment settles it.** A checkout, a saved-card charge or a payment recorded by staff settles the transaction, and the invoice follows it; credit notes, write-offs and late fees adjust it.
