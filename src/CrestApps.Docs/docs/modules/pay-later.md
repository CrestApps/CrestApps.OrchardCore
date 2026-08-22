---
sidebar_label: Pay Later
sidebar_position: 13
title: Pay Later
description: Adds an offline "pay later" payment option to the Orchard Core Checkout framework, reusable by subscriptions and one-time goods purchases, and records the balance as a trackable outstanding transaction.
---

| | |
| --- | --- |
| **Feature Name** | Pay Later |
| **Feature ID** | `CrestApps.OrchardCore.PayLater` |
| **Category** | Commerce |
| **Dependencies** | `CrestApps.OrchardCore.Checkout`, `CrestApps.OrchardCore.Transactions` |

The **Pay Later** module contributes a single, offline payment option to the provider-agnostic [Checkout](checkout) framework. Enabling it makes a *Pay Later* method available to **any** checkout — recurring [Subscriptions](subscriptions) as well as one-time goods purchases — without wiring up an external gateway.

Because Pay Later is a standalone module rather than a per-scenario sub-feature, the same option is defined once and reused everywhere. Enable it alongside whatever purchase module you use, and the option appears automatically.

## What it does

Pay Later records an offline commitment instead of moving money through a processor. It flows through the **exact same durable payment ledger and reconciliation** as a real gateway, so the money-safety guarantees are identical:

- Its verification reports that it is **not** the authoritative source of a charged amount, so the checkout records the commitment on the strength of a recorded transaction id alone — without an amount cross-check.
- It never fabricates a *paid* record that a processor could later contradict.
- In non-production environments it records its commitments in `Testing` gateway mode, mirroring how a real gateway reports its mode.

This makes Pay Later suitable for manual/deferred billing, invoicing, purchase orders, or trials that are confirmed without an immediate card charge.

## Tracking outstanding balances

Because Pay Later never moves money at a gateway, every succeeded Pay Later payment leaves a balance the customer still owes. Pay Later records that balance in the provider-agnostic [Transactions](transactions) ledger when the checkout completes, so an outstanding Pay Later commitment is a first-class, trackable obligation:

- The customer sees it on their **My Transactions** statement and can **pay it online** later (through the Checkout framework) once they are ready.
- An administrator sees it in the **Commerce → Transactions** report alongside every other outstanding balance, and can send a reminder, record an offline payment, mark it paid, cancel it, or add a note.
- The [Transactions](transactions) reminder pipeline chases unpaid Pay Later balances automatically on the configured cadence, through the notification channel each user prefers.

Recording is **idempotent per obligation**, so a checkout that completes more than once never duplicates the debt, and settlement checkouts (paying an existing transaction) never create a new balance.

### Configuring the payment term

Under **Settings → Commerce → Pay Later** you can set the **net term (days)** — how many days after checkout a recorded Pay Later balance is due. The due date drives the reminder cadence configured in the [Transactions](transactions) settings. A value of `0` records the balance with no due date.

## Enabling the feature

Add the package to your Orchard Core project:

```bash
dotnet add package CrestApps.OrchardCore.PayLater
```

Then, in the **Orchard Core Admin Dashboard** under **Tools → Features**, enable **Pay Later**. It brings in the [Checkout](checkout) framework and the [Transactions](transactions) ledger as dependencies. Once enabled, the *Pay Later* option is offered by any checkout on the tenant (for example the [Subscriptions](subscriptions) checkout), and each Pay Later balance is tracked as an outstanding transaction.

## Related modules

- [Checkout](checkout) — the provider-agnostic checkout framework this option plugs into.
- [Transactions](transactions) — the provider-agnostic ledger that tracks, reports, reminds about, and settles the outstanding balances Pay Later records.
- [Subscriptions](subscriptions) — a consumer of checkout that can offer Pay Later at signup.
- [Payments](payments) — the lower-level payment contracts and the Stripe provider.
